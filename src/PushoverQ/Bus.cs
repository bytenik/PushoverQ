using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        #region Publish wrapper overloads
        public Task Publish(object message)
        {
            return Publish(message, Timeout.InfiniteTimeSpan, CancellationToken.None);
        }

        public Task Publish(object message, TimeSpan timeout)
        {
            return Publish(message, timeout, CancellationToken.None);
        }

        public Task Publish(object message, CancellationToken token)
        {
            return Publish(message, Timeout.InfiniteTimeSpan, token);
        }
        #endregion

        public Task Publish(object message, TimeSpan timeout, CancellationToken token)
        {
            return Publish(message, null, timeout, token);
        }

        #region Publish w/ configure wrapper overloads
        public Task Publish(object message, Action<ISendConfigurator> configure)
        {
            return Publish(message, configure, Timeout.InfiniteTimeSpan, CancellationToken.None);
        }

        public Task Publish(object message, Action<ISendConfigurator> configure, TimeSpan timeout)
        {
            return Publish(message, configure, timeout, CancellationToken.None);
        }

        public Task Publish(object message, Action<ISendConfigurator> configure, CancellationToken token)
        {
            return Publish(message, configure, Timeout.InfiniteTimeSpan, token);
        }
        #endregion

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

        #region Publish<T> wrapper overloads
        public Task<T> Publish<T>(object message)
        {
            return Publish<T>(message, Timeout.InfiniteTimeSpan, CancellationToken.None);
        }

        public Task<T> Publish<T>(object message, TimeSpan timeout)
        {
            return Publish<T>(message, timeout, CancellationToken.None);
        }

        public Task<T> Publish<T>(object message, CancellationToken token)
        {
            return Publish<T>(message, Timeout.InfiniteTimeSpan, token);
        }
        #endregion

        public Task<T> Publish<T>(object message, TimeSpan timeout, CancellationToken token)
        {
            return Publish<T>(message, null, timeout, token);
        }

        public Task<T> Publish<T>(object message, Action<ISendConfigurator> configure)
        {
            return Publish<T>(message, configure, Timeout.InfiniteTimeSpan, CancellationToken.None);
        }

        public Task<T> Publish<T>(object message, Action<ISendConfigurator> configure, TimeSpan timeout)
        {
            return Publish<T>(message, configure, timeout, CancellationToken.None);
        }

        public Task<T> Publish<T>(object message, Action<ISendConfigurator> configure, CancellationToken token)
        {
            return Publish<T>(message, configure, Timeout.InfiniteTimeSpan, token);
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
                    MaxSizeInMegabytes = 1024 * 5,
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

        private async Task<Exception> HandleMessage(object message, Envelope envelope)
        {
            if (message == null)
                return null;
            if (envelope == null) throw new ArgumentNullException("envelope");

            Logger.DebugFormat("BEGIN: Handling message with id `{0:n}' and type `{1}'", envelope.MessageId, message.GetType().FullName);

            var types = new HashSet<Type>();
            var type = message.GetType();

            while (type != null)
            {
                types.Add(type);
                type = type.BaseType;
            }

            types.UnionWith(types.SelectMany(t => t.GetInterfaces()).ToArray());

            var handlers = types.SelectMany(t =>
                                                {
                                                    ISet<Func<object, Envelope, Task>> set;
                                                    return _handlers.TryGetValue(t, out set) ? set : Enumerable.Empty<Func<object, Envelope, Task>>();
                                                });

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
                                                       }));
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

        private readonly ConcurrentMultiMap<string, MessageReceiver> _receivers = new ConcurrentMultiMap<string, MessageReceiver>();
        private readonly ConcurrentMultiMap<Type, Func<object, Envelope, Task>> _handlers = new ConcurrentMultiMap<Type, Func<object, Envelope, Task>>();

        public async Task<ISubscription> Subscribe(string topic, string subscription)
        {
            Logger.InfoFormat("Subscribing to topic: `{0}', subscription: `{1}'", topic, subscription);

            await CreateTopic(topic);
            await CreateSubscription(topic, subscription);

            var path = topic + "/subscriptions/" + subscription;
            if (_receivers.CountValues(path) >= _settings.NumberOfReceiversPerSubscription)
                return null;

            var receiver = await RetryPolicy.ExecuteAsync(() => Task<MessageReceiver>.Factory.FromAsync(_messagingFactory.BeginCreateMessageReceiver, _messagingFactory.EndCreateMessageReceiver, path, null));
            _receivers.Add(path, receiver);

            CancellationToken token = new CancellationToken();
            Task.Run(async () =>
                               {
                                   while (true)
                                   {
                                       token.ThrowIfCancellationRequested();

                                       var brokeredMessage = await RetryPolicy.ExecuteAsync(() => Task<BrokeredMessage>.Factory.FromAsync(receiver.BeginReceive, receiver.EndReceive, TimeSpan.FromMinutes(5), null));
                                       if (brokeredMessage == null)
                                           continue; // no message here

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
                                       using (var stream = brokeredMessage.GetBody<Stream>())
                                           message = _settings.Serializer.Deserialize(type, stream);

                                       var envelope = new Envelope { MessageId = Guid.Parse(brokeredMessage.MessageId) };

                                       var ex = await HandleMessage(message, envelope);
                                       if (ex == null)
                                           await RetryPolicy.ExecuteAsync(() => Task.Factory.FromAsync(brokeredMessage.BeginComplete, brokeredMessage.EndComplete, null));
                                       else
                                           await RetryPolicy.ExecuteAsync(() => Task.Factory.FromAsync(brokeredMessage.BeginDeadLetter, brokeredMessage.EndDeadLetter, "A consumer exception occurred", ex.ToString(), null));
                                   }
                               }, token);

            return null;
        }

        public Task<ISubscription> Subscribe(Type type, Func<object, Envelope, Task> handler)
        {
            return Subscribe(_settings.TypeToSubscriptionName(type), type, handler);
        }

        public async Task<ISubscription> Subscribe(string subscription, Type type, Func<object, Envelope, Task> handler)
        {
            Attach(type, handler);
            await Subscribe(subscription, type);

            return null;
        }

        public Task<ISubscription> Subscribe<T>() where T : class
        {
            return Subscribe<T>(_settings.TypeToSubscriptionName(typeof(T)));
        }

        public Task<ISubscription> Subscribe(Type type)
        {
            return Subscribe(_settings.TypeToSubscriptionName(type), type);
        }

        public Task<ISubscription> Subscribe(string subscription, Type type)
        {
            return Subscribe(_settings.TypeToTopicName(type), subscription);
        }

        public Task<ISubscription> Subscribe<T>(string subscription) where T : class
        {
            return Subscribe(subscription, typeof(T));
        }

        public void Attach<T>(Func<T, Task> handler) where T : class
        {
            Attach<T>((m, e) => handler(m));
        }

        public void Attach(Type type, Func<object, Envelope, Task> handler)
        {
            Logger.InfoFormat("Attaching handler for type `{0}'", type);

            Func<object, Envelope, Task> nongeneric = handler;
            _handlers.Add(type, nongeneric);
        }

        public void Attach<T>(Func<T, Envelope, Task> handler) where T : class
        {
            Func<object, Envelope, Task> nongeneric = (m, e) => handler((T)m, e);
            Attach(typeof(T), nongeneric);
        }

        public Task<ISubscription> Subscribe<T>(Func<T, Task> handler) where T : class
        {
            return Subscribe(_settings.TypeToSubscriptionName(typeof(T)), handler);
        }

        public Task<ISubscription> Subscribe<T>(string subscription, Func<T, Task> handler) where T : class
        {
            return Subscribe<T>(subscription, (m, e) => handler(m));
        }

        public Task<ISubscription> Subscribe<T>(Func<T, Envelope, Task> handler) where T : class
        {
            return Subscribe(_settings.TypeToSubscriptionName(typeof(T)), handler);
        }

        public async Task<ISubscription> Subscribe<T>(string subscription, Func<T, Envelope, Task> handler) where T : class
        {
            Attach<T>(handler);
            await Subscribe<T>(subscription);

            return null;
        }

        public Task<ISubscription> Subscribe<T>(Consumes<T>.Message consumer) where T : class
        {
            return Subscribe(_settings.TypeToSubscriptionName(typeof(T)), consumer);
        }

        public Task<ISubscription> Subscribe<T>(string subscription, Consumes<T>.Message consumer) where T : class
        {
            return Subscribe<T>(subscription, consumer.Consume);
        }

        public Task<ISubscription> Subscribe<T>(Consumes<T>.Envelope consumer) where T : class
        {
            return Subscribe(_settings.TypeToSubscriptionName(typeof(T)), consumer);
        }

        public Task<ISubscription> Subscribe<T>(string subscription, Consumes<T>.Envelope consumer) where T : class
        {
            return Subscribe<T>(subscription, consumer.Consume);
        }

        public Task<ISubscription> Subscribe<T>(Func<Consumes<T>.Message> consumerFactory) where T : class
        {
            return Subscribe(_settings.TypeToSubscriptionName(typeof(T)), consumerFactory);
        }

        public Task<ISubscription> Subscribe<T>(string subscription, Func<Consumes<T>.Message> consumerFactory) where T : class
        {
            return Subscribe<T>(subscription, m => consumerFactory().Consume(m));
        }

        public Task<ISubscription> Subscribe<T>(Func<Consumes<T>.Envelope> consumerFactory) where T : class
        {
            return Subscribe(_settings.TypeToSubscriptionName(typeof(T)), consumerFactory);
        }

        public Task<ISubscription> Subscribe<T>(string subscription, Func<Consumes<T>.Envelope> consumerFactory) where T : class
        {
            return Subscribe<T>(subscription, (m, e) => consumerFactory().Consume(m, e));
        }

        #endregion

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
    }
}
