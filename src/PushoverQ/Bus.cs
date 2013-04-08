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
using PushoverQ.RPC;

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

        /// <inheritdoc/>
        public async Task Send(object message, string destination, bool confirmation, TimeSpan? expiration, DateTime? visibleAfter, CancellationToken token)
        {
            if (message == null) throw new ArgumentNullException("message");

            var type = message.GetType();
            if (destination == null) destination = GetTopicNameForCompete(type);
            
            var messageId = Guid.NewGuid();

            Logger.DebugFormat("BEGIN: Waiting to send message of type `{0}` and id `{1:n}' to the bus", message.GetType().FullName, messageId);

            await _publishSemaphore.WaitAsync(token);

            Logger.TraceFormat("GO: Sending message with id `{0:n}` to the bus", messageId);

            if (confirmation) throw new NotImplementedException();

            try
            {
                var sender = await RetryPolicy.ExecuteAsync(() => Task<MessageSender>.Factory.FromAsync(_messagingFactory.BeginCreateMessageSender, _messagingFactory.EndCreateMessageSender, destination, null)
                       .WithCancellation(token));

                await RetryPolicy.ExecuteAsync(async () =>
                {
                    using (var ms = new MemoryStream())
                    {
                        _settings.Serializer.Serialize(message, ms);

                        ms.Seek(0, SeekOrigin.Begin);
                        var brokeredMessage = new BrokeredMessage(ms, false);
                        brokeredMessage.MessageId = messageId.ToString("n");
                        if (visibleAfter != null)
                            brokeredMessage.ScheduledEnqueueTimeUtc = visibleAfter.Value;
                        if (expiration != null)
                            brokeredMessage.TimeToLive = expiration.Value;

                        brokeredMessage.ContentType = message.GetType().AssemblyQualifiedName;
                        await Task.Factory.FromAsync(sender.BeginSend, sender.EndSend, brokeredMessage, null)
                            .WithCancellation(token);
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

        /// <inheritdoc/>
        public async Task<T> Send<T>(object message, string destination, TimeSpan? expiration, DateTime? visibleAfter, CancellationToken token)
        {
            if (message == null) throw new ArgumentNullException("message");

            var type = message.GetType();
            if (destination == null) destination = GetTopicNameForCompete(type);

            var messageId = Guid.NewGuid();

            Logger.DebugFormat("BEGIN: Waiting to send message of type `{0}` and id `{1:n}' to the bus", message.GetType().FullName, messageId);

            await _publishSemaphore.WaitAsync(token);

            Logger.TraceFormat("GO: Sending message with id `{0:n}` to the bus", messageId);

            try
            {
                var sender = await RetryPolicy.ExecuteAsync(() => Task<MessageSender>.Factory.FromAsync(_messagingFactory.BeginCreateMessageSender, _messagingFactory.EndCreateMessageSender, destination, null)
                       .WithCancellation(token));

                var reply = await RetryPolicy.ExecuteAsync(async () =>
                {
                    using (var ms = new MemoryStream())
                    {
                        _settings.Serializer.Serialize(message, ms);

                        ms.Seek(0, SeekOrigin.Begin);
                        var brokeredMessage = new BrokeredMessage(ms, false);
                        brokeredMessage.MessageId = messageId.ToString("n");
                        if (visibleAfter != null)
                            brokeredMessage.ScheduledEnqueueTimeUtc = visibleAfter.Value;
                        if (expiration != null)
                            brokeredMessage.TimeToLive = expiration.Value;

                        brokeredMessage.ReplyTo = _settings.EndpointName;

                        // set up subscription for reply
                        var tcs = new TaskCompletionSource<T>();
                        using (await Subscribe(_settings.EndpointName, async (m, e) =>
                            {
                                if (e.InReplyTo == Guid.Parse(brokeredMessage.MessageId) && m is T)
                                {
                                    tcs.SetResult((T)m);
                                }
                            }))
                        {
                            brokeredMessage.ContentType = message.GetType().AssemblyQualifiedName;
                            await Task.Factory.FromAsync(sender.BeginSend, sender.EndSend, brokeredMessage, null)
                                .WithCancellation(token);

                            return await tcs.Task.WithCancellation(token);
                        }
                    }
                }, token);

                await RetryPolicy.ExecuteAsync(() => Task.Factory.FromAsync(sender.BeginClose, sender.EndClose, null));

                Logger.DebugFormat("END: Sent message with id `{0:n}' to the bus; got reply", messageId);
                return reply;
            }
            finally
            {
                _publishSemaphore.Release();
            }
        }

        /// <inheritdoc/>
        public Task Publish(object message, TimeSpan? expiration, DateTime? visibleAfter, CancellationToken token)
        {
            if (message == null) throw new ArgumentNullException("message");
            
            var type = message.GetType();
            return Send(message, GetTopicNameForPublish(type), false, expiration, visibleAfter, token);
        }

        #endregion

        #region Subscribe

        private async Task CreateQueue(string name)
        {
            Logger.TraceFormat("Creating queue {0}", name);

            try
            {
                var qd = new QueueDescription(name)
                {
                    EnableBatchedOperations = true,
                    IsAnonymousAccessible = false,
                    MaxSizeInMegabytes = 1024 * 5, // max size allowed by bus is 5 GB
                    RequiresDuplicateDetection = true,
                    RequiresSession = false,
                };
                await RetryPolicy.ExecuteAsync(() => Task<QueueDescription>.Factory.FromAsync(_namespaceManager.BeginCreateQueue, _namespaceManager.EndCreateQueue, qd, null));
            }
            catch (MessagingEntityAlreadyExistsException)
            {
                Logger.TraceFormat("Topic {0} already exists", name);
            }
        }

        private async Task CreateTopic(string name)
        {
            Logger.TraceFormat("Creating topic {0}", name);

            try
            {
                var td = new TopicDescription(name)
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
                Logger.TraceFormat("Topic {0} already exists", name);
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

        private async Task<Exception> HandleMessage(object message, Envelope envelope, IEnumerable<Func<object, Envelope, Task>> handlers)
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

        public async Task<ISubscription> Subscribe(string topic, string subscription, Func<object, Envelope, Task> handler)
        {
            Logger.InfoFormat("Subscribing to topic: `{0}' subscription: `{1}'", topic, subscription);

            await CreateTopic(topic);

            await CreateSubscription(topic, subscription);
            var path = GetPath(topic, subscription);

            return await Subscribe(path, handler);
        }

        public async Task<ISubscription> Subscribe(string path, Func<object, Envelope, Task> handler)
        {
            Logger.InfoFormat("Subscribing to path: `{0}'", path);

            for (var i = _pathToReceivers.CountValues(path); i < _settings.NumberOfReceiversPerSubscription; i++)
                await SpinUpReceiver(path);

            return null;
        }

        private string GetTopicNameForCompete(Type type)
        {
            return type.FullName + "..Compete";
        }

        private string GetTopicNameForPublish(Type type)
        {
            return type.FullName;
        }

        public async Task<ISubscription> Subscribe(Type type, Func<object, Envelope, Task> handler)
        {
            var tasks = new[]
                {
                    Subscribe(GetTopicNameForPublish(type), _settings.EndpointName, handler),
                    Subscribe(GetTopicNameForCompete(type), _settings.ApplicationName, handler)
                };

            var subscriptions = await Task.WhenAll(tasks);
            return new CompositeSubscription(subscriptions);
        }

        public Task<ISubscription> Subscribe<T>(Func<T, Envelope, Task> handler) where T : class
        {
            return Subscribe(typeof(T), (m, e) => m is T ? handler((T)m, e) : null);
        }

        #endregion

        public T GetProxy<T>()
        {
            return new RPC.Proxy<T>(this).GetTransparentProxy();
        }

        public async Task<ISubscription> Subscribe<T>(Func<T> resolver)
        {
            var type = typeof(T);
            Func<object, Envelope, Task> handler = async (m, e) =>
                {
                    var impl = resolver();
                    var msg = m as MethodCallCommand;
                    if (msg == null) return;

                    var mi = type.GetMethod(msg.MethodName, msg.ArgumentTypes.Select(Type.GetType).ToArray());
                    var returnValue = mi.Invoke(impl, msg.Arguments);

                    if (returnValue is Task) await (Task)returnValue;

                    // todo: reply here if needed
                };

            var types = type.GetInterfaces();
            var subscriptionTasks = types.SelectMany(x => new[]
                {
                    Subscribe(GetTopicNameForCompete(type), _settings.ApplicationName, handler),
                    Subscribe(GetTopicNameForPublish(type), _settings.EndpointName, handler)
                });

            return new CompositeSubscription(await Task.WhenAll(subscriptionTasks));
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
