using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Microsoft.Practices.TransientFaultHandling;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using PushoverQ.Configuration;
using PushoverQ.ContextConfiguration;
using PushoverQ.SendConfiguration;

namespace PushoverQ
{
    public sealed class Bus : IBus, IDisposable
    {
        private readonly BusSettings _settings;
        private readonly NamespaceManager _namespaceManager;
        private readonly MessagingFactory _messagingFactory;
        private readonly SemaphoreSlim _publishSemaphore;
        private static readonly RetryPolicy RetryPolicy = new RetryPolicy<TransientErrorDetectionStrategy>(
            new ExponentialBackoff("Retry exponentially", int.MaxValue, TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(30), true));

        private static readonly ILog Logger = LogManager.GetCurrentClassLogger();

        public static async Task<IBus> CreateBus(Action<BusConfigurator> configure)
        {
            var configurator = new BusConfigurator();
            configure(configurator);

            var bus = new Bus(configurator.Settings);
            await bus.Start();
            return bus;
        }

        private Bus(BusSettings settings)
        {
            _settings = settings;

            _messagingFactory = MessagingFactory.CreateFromConnectionString(settings.ConnectionString);
            _namespaceManager = NamespaceManager.CreateFromConnectionString(settings.ConnectionString);
            _publishSemaphore = new SemaphoreSlim(settings.MaxMessagesInFlight);
        }

        private async Task Start()
        {
        }

        #region Publish

        public Task Publish(object message, TimeSpan timeout, CancellationToken token)
        {
            return Publish(message, null, timeout, token);
        }

        public async Task Publish(object message, Action<ISendConfigurator> configure, TimeSpan timeout, CancellationToken token)
        {
            var configurator = new SendConfigurator();
            configurator.ToTopic(_settings.TypeToTopicName(message.GetType()));
            if (configure != null) configure(configurator);
            var sendSettings = configurator.SendSettings;

            if (sendSettings.NeedsConfirmation)
                throw new NotImplementedException();

            var messageId = Guid.NewGuid();

            Logger.DebugFormat("BEGIN: Waiting to send message of type `{0}` and id `{1:n}' to the bus", message.GetType().FullName, messageId);

            await _publishSemaphore.WaitAsync(timeout, token);

            Logger.TraceFormat("GO: Sending message with id `{0:n}` to the bus", messageId);

            try
            {
                var sender = await RetryPolicy.ExecuteAsync(() => Task<MessageSender>.Factory.FromAsync(_messagingFactory.BeginCreateMessageSender, _messagingFactory.EndCreateMessageSender, sendSettings.Topic, null)
                       .WithTimeoutAndCancellation(timeout, token));

                await RetryPolicy.ExecuteAsync(async () =>
                                                         {
                                                             using (var ms = new MemoryStream())
                                                             {
                                                                 _settings.Serializer.Serialize(message, ms);

                                                                 ms.Seek(0, SeekOrigin.Begin);
                                                                 var brokeredMessage = new BrokeredMessage(ms, false);
                                                                 brokeredMessage.MessageId = messageId.ToString("n");
                                                                 if (sendSettings.VisibleAfter != null)
                                                                     brokeredMessage.ScheduledEnqueueTimeUtc = sendSettings.VisibleAfter.Value;
                                                                 if (sendSettings.Expiration != null)
                                                                     brokeredMessage.TimeToLive = sendSettings.Expiration.Value;

                                                                 brokeredMessage.ContentType = message.GetType().AssemblyQualifiedName;
                                                                 await Task.Factory.FromAsync(sender.BeginSend, sender.EndSend, brokeredMessage, null)
                                                                     .WithTimeoutAndCancellation(timeout, token);
                                                             }
                                                         }, token);

                await RetryPolicy.ExecuteAsync(() => Task.Factory.FromAsync(sender.BeginClose, sender.EndClose, null));

                Logger.DebugFormat("END: Sent message with id `{0:n}' to the bus", messageId);
            }
            finally
            {
                _publishSemaphore.Release();
            }
        }

        public Task<T> Publish<T>(object message, TimeSpan timeout, CancellationToken token)
        {
            return Publish<T>(message, null, timeout, token);
        }

        public Task<T> Publish<T>(object message, Action<ISendConfigurator> configure, TimeSpan timeout, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Subscribe

        private async Task CreateTopic(string topic)
        {
            Logger.TraceFormat("Creating topic {0}", topic);

            try
            {
                var td = new TopicDescription(topic)
                {
                    EnableBatchedOperations = true,
                    IsAnonymousAccessible = false,
                    MaxSizeInMegabytes = 1024 * 5, // max size allowed by bus is 5 GB
                    RequiresDuplicateDetection = true,
                };
                await RetryPolicy.ExecuteAsync(() => Task<TopicDescription>.Factory.FromAsync(_namespaceManager.BeginCreateTopic, _namespaceManager.EndCreateTopic, td, null));
            }
            catch (MessagingEntityAlreadyExistsException)
            {
                Logger.TraceFormat("Topic {0} already exists", topic);
            }
        }

        private async Task CreateSubscription(string topic, string subscription)
        {
            Logger.TraceFormat("Creating subscription {0} for topic {1}", subscription, topic);

            try
            {
                var sd = new SubscriptionDescription(topic, subscription)
                {
                    RequiresSession = false,
                    EnableBatchedOperations = true
                };
                await RetryPolicy.ExecuteAsync(() => Task<SubscriptionDescription>.Factory.FromAsync(_namespaceManager.BeginCreateSubscription, _namespaceManager.EndCreateSubscription, sd, null));
            }
            catch (MessagingEntityAlreadyExistsException)
            {
                Logger.TraceFormat("Subscription {0} for topic {1} already exists", subscription, topic);
            }
        }

        private async Task<Exception> HandleMessage(object message, Envelope envelope, ISet<Func<object, Envelope, Task>> handlers)
        {
            if (message == null)
                return null;
            if (envelope == null) throw new ArgumentNullException("envelope");

            Logger.DebugFormat("BEGIN: Handling message with id `{0:n}' and type `{1}'", envelope.MessageId, message.GetType().FullName);

            try
            {
                await Task.WhenAll(handlers.Select(h =>
                    {
                        try
                        {
                            return h(message, envelope);
                        }
                        catch (Exception ex)
                        {
                            return ex.AsTask();
                        }
                    })
                    .Where(t => t != null));
            }
            catch (AggregateException ae)
            {
                foreach (var ex in ae.InnerExceptions)
                    Logger.WarnFormat("A consumer exception occurred handling message `{0:n}'", ex, envelope.MessageId);
                return ae;
            }
            catch (Exception ex)
            {
                Logger.WarnFormat("A consumer exception occurred handling message `{0:n}'", ex, envelope.MessageId);
                return ex;
            }

            Logger.DebugFormat("END: Handled message with id `{0:n}'", envelope.MessageId);

            return null;
        }

        private readonly ConcurrentMultiMap<string, MessageReceiver> _pathToReceivers = new ConcurrentMultiMap<string, MessageReceiver>();
        private readonly ConcurrentMultiMap<string, Func<object, Envelope, Task>> _pathToHandlers = new ConcurrentMultiMap<string, Func<object, Envelope, Task>>();

        private async Task<MessageReceiver> SpinUpReceiver(string path)
        {
            var receiver = await RetryPolicy.ExecuteAsync(() => Task<MessageReceiver>.Factory.FromAsync(_messagingFactory.BeginCreateMessageReceiver, _messagingFactory.EndCreateMessageReceiver, path, null));
            _pathToReceivers.Add(path, receiver);

            Task.Run(async () =>
                {
                    while (true)
                    {
                        var brokeredMessage = await RetryPolicy.ExecuteAsync(() => Task<BrokeredMessage>.Factory.FromAsync(receiver.BeginReceive, receiver.EndReceive, TimeSpan.FromMinutes(5), null));
                        if (brokeredMessage == null) continue; // no message here

                        if (brokeredMessage.ContentType == null)
                        {
                            await RetryPolicy.ExecuteAsync(() => Task.Factory.FromAsync(brokeredMessage.BeginDeadLetter, brokeredMessage.EndDeadLetter, null));
                            continue;
                        }

                        var type = Type.GetType(brokeredMessage.ContentType, false);
                        if (type == null)
                        {
                            await RetryPolicy.ExecuteAsync(() => Task.Factory.FromAsync(brokeredMessage.BeginDeadLetter, brokeredMessage.EndDeadLetter, null));
                            continue;
                        }

                        object message;
                        using (var stream = brokeredMessage.GetBody<Stream>()) message = _settings.Serializer.Deserialize(type, stream);

                        var envelope = new Envelope {MessageId = Guid.Parse(brokeredMessage.MessageId)};

                        var ex = await HandleMessage(message, envelope, _pathToHandlers[path]);

                        try
                        {
                            if (ex == null) await RetryPolicy.ExecuteAsync(() => Task.Factory.FromAsync(brokeredMessage.BeginComplete, brokeredMessage.EndComplete, null));
                            else await RetryPolicy.ExecuteAsync(() => Task.Factory.FromAsync(brokeredMessage.BeginDeadLetter, brokeredMessage.EndDeadLetter, "A consumer exception occurred", ex.ToString(), null));
                        }
                        catch (MessageLockLostException)
                        {
                            // oh well...
                        }
                    }
                });

            return receiver;
        }

        private class Subscription : ISubscription
        {
            private readonly IBus _bus;

            public Subscription(IBus bus)
            {
                _bus = bus;
            }

            public void Dispose()
            {
                try
                {
                    Unsubscribe().Wait();
                }
                catch (AggregateException e)
                {
                    if (e.InnerExceptions.Count == 1)
                        throw e.InnerException;
                    
                    throw;
                }
            }

            public Task Unsubscribe()
            {
                throw new NotImplementedException();
            }
        }

        private string GetPath(string topic, string subscription)
        {
            if (topic == null) throw new ArgumentNullException("topic");
            if (subscription == null) throw new ArgumentNullException("subscription");

            return topic + "/subscriptions/" + subscription;
        }

        public async Task<ISubscription> Subscribe(string topic, IEnumerable<string> subscriptions, Func<object, Envelope, Task> handler)
        {
            Logger.InfoFormat("Subscribing to topic: `{0}'", topic);

            await CreateTopic(topic);

            var subscribeTasks = subscriptions.Select<string, Task>(async subscription =>
            {
                await CreateSubscription(topic, subscription);
                var path = GetPath(topic, subscription);

                for (var i = _pathToReceivers.CountValues(path); i < _settings.NumberOfReceiversPerSubscription; i++)
                    await SpinUpReceiver(path);
            });

            await Task.WhenAll(subscribeTasks);
            return null;
        }

        private string GetTopicName(Type type)
        {
            return type.FullName;
        }

        public Task<ISubscription> Subscribe(Type type, Func<object, Envelope, Task> handler)
        {
            var types = new HashSet<Type>();

            while (type != null)
            {
                types.Add(type);
                type = type.BaseType;
            }

            types.UnionWith(types.SelectMany(t => t.GetInterfaces()).ToArray());

            return Subscribe(types.Select(x => GetTopicName(type)), type, handler);
        }

        public Task<ISubscription> Subscribe(string subscription, Type type, Func<object, Envelope, Task> handler)
        {
            return Subscribe(subscription, _settings.TypeToTopicName(type), handler);
        }

        public Task<ISubscription> Subscribe<T>(Func<T, Envelope, Task> handler) where T : class
        {
            return Subscribe(typeof(T), (m, e) => m is T ? handler((T)m, e) : null);
        }

        public Task<ISubscription> Subscribe<T>(string subscription, Func<T, Envelope, Task> handler) where T : class
        {
            return Subscribe(subscription, typeof(T), (m, e) => m is T ? handler((T)m, e) : null);
        }

        #endregion

        public T GetProxy<T>()
        {
            return new RPC.Proxy<T>(this).GetTransparentProxy();
        }

        public Task<ISubscription> Subscribe<T>(Func<T> resolver)
        {
            throw new NotImplementedException();
        }

        #region Disposal

        public void Dispose(bool disposing)
        {

        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~Bus()
        {
            Dispose(false);
        }

        #endregion
    }
}
