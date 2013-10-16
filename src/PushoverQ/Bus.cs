using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Practices.TransientFaultHandling;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

using PushoverQ.Configuration;
using PushoverQ.RPC;

using RetryPolicy = Microsoft.Practices.TransientFaultHandling.RetryPolicy;

namespace PushoverQ
{
    /// <summary>
    /// The PushoverQ bus.
    /// </summary>
    public sealed class Bus : IBus, IDisposable
    {
        private readonly BusSettings _settings;
        private readonly NamespaceManager _nm;
        private readonly MessagingFactory _mf;
        private readonly SemaphoreSlim _publishSemaphore;
        private static readonly RetryPolicy RetryPolicy
            = new RetryPolicy<TransientErrorDetectionStrategy>(new ExponentialBackoff("Retry exponentially", int.MaxValue, TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(30), true));

        /// <summary>
        /// Gets the logger.
        /// </summary>
        public ILog Logger { get { return _settings.Logger; } }

        /// <summary>
        /// Creates an active service bus instance.
        /// </summary>
        /// <param name="configure"> The configuration delegate used to configure the bus. </param>
        /// <returns> The new bus instance. </returns>
        public static Task<IBus> CreateBus(Action<BusConfigurator> configure)
        {
            var configurator = new BusConfigurator();
            configure(configurator);

            var bus = new Bus(configurator.Settings);
            return Task.FromResult<IBus>(bus);
        }

        private Bus(BusSettings settings)
        {
            _settings = settings;

            _mf = MessagingFactory.CreateFromConnectionString(settings.ConnectionString);
            _mf.RetryPolicy = new NoRetry(); // we retry in application logic

            _nm = NamespaceManager.CreateFromConnectionString(settings.ConnectionString);
            
            _publishSemaphore = new SemaphoreSlim(settings.MaxMessagesInFlight);
        }

        #region Publish

        /// <inheritdoc/>
        public async Task Send(object message, string destination, bool confirmation, TimeSpan? expiration, DateTime? visibleAfter, CancellationToken token)
        {
            if (message == null) throw new ArgumentNullException("message");

            var type = message.GetType();
            if (destination == null) destination = GetTopicName(type);
            
            var messageId = Guid.NewGuid();

            Logger.Debug("BEGIN: Waiting to send message of messageType `{0}` and id `{1:n}' to the bus", message.GetType().FullName, messageId);

            await _publishSemaphore.WaitAsync(token);

            Logger.Trace("GO: Sending message with id `{0:n}` to the bus", messageId);

            if (confirmation) throw new NotImplementedException();

            try
            {
                var sender = _mf.CreateTopicClient(destination);

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
                        await sender.SendAsync(brokeredMessage).WithCancellation(token);
                    }
                }, token);

                await RetryPolicy.ExecuteAsync(() => sender.CloseAsync(), token);

                Logger.Debug("END: Sent message with id `{0:n}' to the bus", messageId);
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
            if (destination == null) destination = GetTopicName(type);

            var messageId = Guid.NewGuid();

            Logger.Debug("BEGIN: Waiting to send message of messageType `{0}` and id `{1:n}' to the bus", message.GetType().FullName, messageId);

            await _publishSemaphore.WaitAsync(token);

            Logger.Trace("GO: Sending message with id `{0:n}` to the bus", messageId);

            try
            {
                var sender = await RetryPolicy.ExecuteAsync(() => _mf.CreateMessageSenderAsync(destination).WithCancellation(token), token);

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
                            await sender.SendAsync(brokeredMessage).WithCancellation(token);

                            return await tcs.Task.WithCancellation(token);
                        }
                    }
                }, token);

                await RetryPolicy.ExecuteAsync(() => sender.CloseAsync(), token);

                Logger.Debug("END: Sent message with id `{0:n}' to the bus; got reply", messageId);
                return reply;
            }
            finally
            {
                _publishSemaphore.Release();
            }
        }

        /// <inheritdoc/>
        public T GetProxy<T>()
        {
            return new RPC.Proxy<T>(this).GetTransparentProxy();
        }

        #endregion

        #region Subscribe

        private async Task CreateQueue(string name)
        {
            Logger.Trace("Creating queue {0}", name);

            try
            {
                var qd = new QueueDescription(name)
                {
                    EnableBatchedOperations = true,
                    IsAnonymousAccessible = false,
                    MaxSizeInMegabytes = 1024 * 5, // max size allowed by bus is 5 GB
                    RequiresDuplicateDetection = true,
                    RequiresSession = false,
                    AutoDeleteOnIdle = TimeSpan.FromMinutes(30)
                };
                await RetryPolicy.ExecuteAsync(() => _nm.CreateQueueAsync(qd));
            }
            catch (MessagingEntityAlreadyExistsException)
            {
                Logger.Trace("Topic {0} already exists", name);
            }
        }

        private async Task CreateTopic(string name)
        {
            Logger.Trace("Creating topic {0}", name);

            try
            {
                var td = new TopicDescription(name)
                {
                    EnableBatchedOperations = true,
                    IsAnonymousAccessible = false,
                    MaxSizeInMegabytes = 1024 * 5, // max size allowed by bus is 5 GB
                    RequiresDuplicateDetection = true,
                    AutoDeleteOnIdle = TimeSpan.FromMinutes(30)
                };
                await RetryPolicy.ExecuteAsync(() => _nm.CreateTopicAsync(td));
            }
            catch (MessagingEntityAlreadyExistsException)
            {
                Logger.Trace("Topic {0} already exists", name);
            }
        }

        private async Task CreateSubscription(string topic, string subscription)
        {
            Logger.Trace("Creating subscription {0} for topic {1}", subscription, topic);

            try
            {
                var sd = new SubscriptionDescription(topic, subscription)
                {
                    RequiresSession = false,
                    EnableBatchedOperations = true,
                    AutoDeleteOnIdle = TimeSpan.FromMinutes(30)
                };
                await RetryPolicy.ExecuteAsync(() => _nm.CreateSubscriptionAsync(sd));
            }
            catch (MessagingEntityAlreadyExistsException)
            {
                Logger.Trace("Subscription {0} for topic {1} already exists", subscription, topic);
            }
        }

        private async Task<Exception> HandleMessage(object message, Envelope envelope, IEnumerable<Func<object, Envelope, Task>> handlers)
        {
            if (message == null) return null;
            if (envelope == null) throw new ArgumentNullException("envelope");

            Logger.Debug("BEGIN: Handling message with id `{0:n}' and messageType `{1}'", envelope.MessageId, message.GetType().FullName);

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
                    Logger.Warn("A consumer exception occurred handling message `{0:n}'", ex, envelope.MessageId);
                return ae;
            }
            catch (Exception ex)
            {
                Logger.Warn("A consumer exception occurred handling message `{0:n}'", ex, envelope.MessageId);
                return ex;
            }

            Logger.Debug("END: Handled message with id `{0:n}'", envelope.MessageId);

            return null;
        }

        private readonly ConcurrentMultiMap<string, CancellationTokenSource> _pathToReceiverCancelSources = new ConcurrentMultiMap<string, CancellationTokenSource>();
        private readonly ConcurrentMultiMap<string, Func<object, Envelope, Task>> _pathToHandlers = new ConcurrentMultiMap<string, Func<object, Envelope, Task>>();

        private async Task<MessageReceiver> SpinUpReceiver(string path)
        {
            var receiver = await RetryPolicy.ExecuteAsync(() => _mf.CreateMessageReceiverAsync(path));
            
            var cts = new CancellationTokenSource();
            _pathToReceiverCancelSources.Add(path, cts);

            var token = cts.Token;

            Task.Run(async () =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var brokeredMessage = await RetryPolicy.ExecuteAsync(() => receiver.ReceiveAsync(TimeSpan.FromMinutes(5)), token);
                            if (brokeredMessage == null) continue; // no message here

                            var stopwatch = Stopwatch.StartNew();

                            if (brokeredMessage.ContentType == null)
                            {
                                await RetryPolicy.ExecuteAsync(() => brokeredMessage.DeadLetterAsync(), token);
                                continue;
                            }

                            var type = Type.GetType(brokeredMessage.ContentType, false);
                            if (type == null)
                            {
                                await RetryPolicy.ExecuteAsync(() => brokeredMessage.DeadLetterAsync(), token);
                                continue;
                            }

                            object message;
                            using (var stream = brokeredMessage.GetBody<Stream>()) message = _settings.Serializer.Deserialize(type, stream);

                            var envelope = new Envelope
                            {
                                MessageId = Guid.Parse(brokeredMessage.MessageId),
                                SequenceNumber = brokeredMessage.SequenceNumber
                            };

                            var ex = await HandleMessage(message, envelope, _pathToHandlers[path]);

                            try
                            {
                                if (ex == null)
                                    await RetryPolicy.ExecuteAsync(() => brokeredMessage.CompleteAsync(), token);
                                else
                                    await RetryPolicy.ExecuteAsync(() => brokeredMessage.DeadLetterAsync("A consumer exception occurred", ex.ToString()), token);
                            }
                            catch (MessageLockLostException)
                            {
                                Logger.Warn("Lost lock on message of type {0}, after {1} processing time; the message will be available again for dequeueing", type, stopwatch.Elapsed);
                            }
                        }
                        catch (MessagingEntityNotFoundException)
                        {
                            Logger.Warn("Receiver for path {0} shut down because the messaging entity was deleted.", path);
                        }
                        catch (OperationCanceledException e)
                        {
                            if (e.CancellationToken != token)
                                Logger.Fatal(e, "Receiver for path {0} shut down due to unhandled exception in the message pump; this indicates a bug in PushoverQ", path);
                        }
                        catch (Exception e)
                        {
                            Logger.Fatal(e, "Receiver for path {0} shut down due to unhandled exception in the message pump; this indicates a bug in PushoverQ", path);
                        }
                    }

                    await RetryPolicy.ExecuteAsync(() => receiver.CloseAsync());

                    try
                    {
                        cts.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // don't care
                    }

                    _pathToReceiverCancelSources.Remove(path, cts);

                    Logger.Debug("Receiver for path {0} shut down gracefully.", path);

                    throw new OperationCanceledException(token);
                }, token);

            return receiver;
        }

        private string GetPath(string topic, string subscription)
        {
            if (topic == null) throw new ArgumentNullException("topic");
            if (subscription == null) throw new ArgumentNullException("subscription");

            return topic + "/subscriptions/" + subscription;
        }

        /// <inheritdoc/>
        public Task<ISubscription> Subscribe(Type messageType, string subscription, Func<object, Envelope, Task> handler)
        {
            return Subscribe(GetTopicName(messageType), subscription, handler);
        }

        /// <inheritdoc/>
        public async Task<ISubscription> Subscribe(string topic, string subscription, Func<object, Envelope, Task> handler)
        {
            Logger.Info("Subscribing to topic: `{0}' subscription: `{1}'", topic, subscription);

            await CreateTopic(topic);
            await CreateSubscription(topic, subscription);
            
            var path = GetPath(topic, subscription);

            return await Subscribe(path, handler);
        }

        /// <inheritdoc/>
        public async Task<ISubscription> Subscribe(string path, Func<object, Envelope, Task> handler)
        {
            Logger.Info("Subscribing to path: `{0}'", path);

            for (var i = _pathToReceiverCancelSources.CountValues(path); i < _settings.NumberOfReceiversPerSubscription; i++)
                await SpinUpReceiver(path);

            _pathToHandlers.Add(path, handler);

            return new DelegateSubscription(async () =>
                    {
                        _pathToHandlers.Remove(path, handler);

                        // if there's still other handlers, keep the receiver alive
                        if (_pathToHandlers.CountValues(path) != 0) return;

                        // shut down all receivers
                        var cancellationSources = _pathToReceiverCancelSources[path].ToArray();
                        foreach (var cts in cancellationSources)
                        {
                            cts.Cancel();
                            cts.Dispose();
                            _pathToReceiverCancelSources.Remove(path, cts);
                        }
                    });
        }

        private string GetTopicName(Type messageType)
        {
            return _settings.TopicNameResolver(messageType);
        }

        private string GetSubscriptionName(Type consumerType)
        {
            return _settings.ApplicationName.Right(50);
        }

        /// <inheritdoc/>
        public Task<ISubscription> Subscribe(Type messageType, Func<object, Envelope, Task> handler)
        {
            return Subscribe(GetTopicName(messageType), _settings.EndpointName, handler);
        }

        /// <inheritdoc/>
        public Task<ISubscription> Subscribe<T>(Func<T, Envelope, Task> handler) where T : class
        {
            return Subscribe(typeof(T), (m, e) => m is T ? handler((T)m, e) : null);
        }

        /// <inheritdoc/>
        public Task<ISubscription> Subscribe<T>(string subscription, Func<T, Envelope, Task> handler) where T : class
        {
            return Subscribe<T>(GetTopicName(typeof(T)), subscription, handler);
        }

        /// <inheritdoc/>
        public Task<ISubscription> Subscribe<T>(string topic, string subscription, Func<T, Envelope, Task> handler) where T : class
        {
            // todo: replace null
            return Subscribe<T>(GetSubscriptionName(null), handler);
        }

        /// <inheritdoc/>
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

                if (returnValue is Task) await(Task)returnValue;

                // todo: reply here if needed
            };

            var types = type.GetInterfaces();
            var subscriptionTasks = types.SelectMany(x => new[]
                {
                    Subscribe(GetTopicName(type), _settings.ApplicationName, handler),
                });

            return new CompositeSubscription(_settings.Logger, await Task.WhenAll(subscriptionTasks));
        }

        #endregion

        #region Disposal

        bool _disposed = false;

        /// <summary>
        /// Disposes of the bus.
        /// </summary>
        /// <param name="disposing"> true if disposing; false if finalizing. </param>
        public void Dispose(bool disposing)
        {
            if (_disposed) throw new ObjectDisposedException(typeof(Bus).Name);

            foreach (var cts in _pathToReceiverCancelSources.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }

            _disposed = true;
        }

        /// <inheritdoc/>
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
