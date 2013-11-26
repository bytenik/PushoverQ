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
        private readonly RetryPolicy RetryPolicy;
    
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

            RetryPolicy = new RetryPolicy<TransientErrorDetectionStrategy>(new ExponentialBackoff("Retry exponentially", int.MaxValue, TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(30), true));
            var strategy = (TransientErrorDetectionStrategy)RetryPolicy.ErrorDetectionStrategy;
            strategy.Logger = _settings.Logger;

            _mf = MessagingFactory.CreateFromConnectionString(settings.ConnectionString);
            _mf.RetryPolicy = new NoRetry(); // we use our own retry logic

            _nm = NamespaceManager.CreateFromConnectionString(settings.ConnectionString);
            
            _publishSemaphore = new SemaphoreSlim(settings.MaxMessagesInFlight);
        }

        #region Publish

        /// <inheritdoc/>
        public Task Send(object message, string destination, bool confirmation, TimeSpan? expiration, DateTime? visibleAfter, CancellationToken token)
        {
            if (message == null) throw new ArgumentNullException("message");
            
            return Send(new[] { message }, destination, confirmation, expiration, visibleAfter, token);
        }

        /// <inheritdoc/>
        public async Task Send(IEnumerable<object> messages, string destination, bool confirmation, TimeSpan? expiration, DateTime? visibleAfter, CancellationToken token)
        {
            if (messages == null) throw new ArgumentNullException("messages");

            var typesWithMessages = messages.GroupBy(x => x.GetType()).ToArray();
            if (typesWithMessages.Length > 1)
            {
                var tasks = typesWithMessages.Select(batch => Send(batch, destination, confirmation, expiration, visibleAfter, token));
                await Task.WhenAll(tasks);
                return;
            }

            var type = typesWithMessages.Single().Key;
            if (destination == null) destination = GetTopicName(type);

            var messagesWithIds = messages.Select(x => new
            {
                Message = x,
                Id = Guid.NewGuid()
            }).ToArray(); // must ToArray this or else you will get a new id for each evaluation

            Logger.Debug("BEGIN: Waiting to send messages of messageType `{0}' and ids `{1}' to the bus", type.FullName, string.Join("; ", messagesWithIds.Select(x => x.Id.ToString("n"))));

            await _publishSemaphore.WaitAsync(token);

            if (confirmation) throw new NotImplementedException();

            Logger.Trace("GO: Sending messages with ids `{0}' to the bus", string.Join("; ", messagesWithIds.Select(x => x.Id.ToString("n"))));

            try
            {
                var sender = _mf.CreateTopicClient(destination);

                await RetryPolicy.ExecuteAsync(async () =>
                {
                    var brokeredMessages = new List<BrokeredMessage>();
                    foreach (var mi in messagesWithIds)
                    {
                        var ms = new MemoryStream(); // do not wrap this with a using statement; the BrokeredMessage owns the stream and will dispose it
                        _settings.Serializer.Serialize(mi.Message, ms);

                        ms.Seek(0, SeekOrigin.Begin);
                        if (ms.Length > 255 * 1024)
                        {
                            var message = string.Format("Message larger than maximum size of 262144 bytes. Size: {0} bytes.", ms.Length);
                            if (_settings.ThrowOnOversizeMessage) throw new MessageSizeException(message);
                            Logger.Debug(new MessageSizeException(), message);
                            continue;
                        }

                        var brokeredMessage = new BrokeredMessage(ms, true)
                        {
                            MessageId = mi.Id.ToString("n")
                        };
                        if (visibleAfter != null)
                            brokeredMessage.ScheduledEnqueueTimeUtc = visibleAfter.Value;
                        if (expiration != null)
                            brokeredMessage.TimeToLive = expiration.Value;

                        brokeredMessage.ContentType = mi.Message.GetType().AssemblyQualifiedName;
                        brokeredMessages.Add(brokeredMessage);
                    }

                    try
                    {
                        await sender.SendBatchAsync(brokeredMessages).WithCancellation(token);
                    }
                    finally
                    {
                        // always dispose the brokered messages
                        foreach (var brokeredMessage in brokeredMessages)
                        {
                            if (brokeredMessage.Size > 255 * 1024)
                            {
                                var message = string.Format("Message was determined to be too large after send attempt ({0} bytes), delivery not guaranteed", brokeredMessage.Size);
                                if (_settings.ThrowOnOversizeMessage) throw new MessageSizeException(message);
                                Logger.Debug(new MessageSizeException(), message);
                            }

                            brokeredMessage.Dispose();
                        }
                    }
                }, token);

                try
                {
                    await RetryPolicy.ExecuteAsync(() => sender.CloseAsync(), token);
                }
                catch
                {
                    // no-op
                }

                Logger.Debug("END: Sent messages with ids `{0}'", string.Join("; ", messagesWithIds.Select(x => x.Id.ToString("n"))));
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
                sender.RetryPolicy = new NoRetry(); // use our retry policy please

                var reply = await RetryPolicy.ExecuteAsync(async () =>
                {
                    using (var ms = new MemoryStream())
                    {
                        _settings.Serializer.Serialize(message, ms);
                        ms.Seek(0, SeekOrigin.Begin);
                        if (ms.Length > 255 * 1024)
                        {
                            var exceptionMessage = string.Format("Message larger than maximum size of 262144 bytes. Size: {0} bytes.", ms.Length);
                            if (_settings.ThrowOnOversizeMessage) throw new MessageSizeException(exceptionMessage);
                            Logger.Debug(new MessageSizeException(), exceptionMessage);
                            return default(T);
                        }

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

        async Task CreateTopic(string name)
        {
            Logger.Trace("Creating topic {0}", name);

            var td = new TopicDescription(name)
            {
                EnableBatchedOperations = true,
                IsAnonymousAccessible = false,
                MaxSizeInMegabytes = 1024 * 5, // max size allowed by bus is 5 GB
                RequiresDuplicateDetection = true,
                AutoDeleteOnIdle = TimeSpan.FromMinutes(30)
            };

            if (await _nm.TopicExistsAsync(name))
            {
                try
                {
                    await RetryPolicy.ExecuteAsync(() => _nm.UpdateTopicAsync(td));
                    return;
                }
                catch (MessagingEntityNotFoundException e)
                {
                    Logger.Warn(e, "Topic {0} is not found (but was just found seconds earlier)", name);
                }
            }
            else
            {
                try
                {
                    await RetryPolicy.ExecuteAsync(() => _nm.CreateTopicAsync(td));
                    return;
                }
                catch (MessagingEntityAlreadyExistsException e)
                {
                    Logger.Trace(e, "Topic {0} already exists", name);
                }
            }

            // wtf
            await CreateTopic(name);
        }

        private async Task CreateSubscription(string topic, string subscription)
        {
            Logger.Trace("Creating subscription {0} for topic {1}", subscription, topic);

            var sd = new SubscriptionDescription(topic, subscription)
            {
                RequiresSession = false,
                EnableBatchedOperations = true,
                AutoDeleteOnIdle = TimeSpan.FromMinutes(30),
                LockDuration = _settings.LockDuration,
                MaxDeliveryCount = (int)_settings.MaxDeliveryCount
            };

            if (await _nm.SubscriptionExistsAsync(topic, subscription))
            {
                try
                {
                    await RetryPolicy.ExecuteAsync(() => _nm.UpdateSubscriptionAsync(sd));
                    return;
                }
                catch (MessagingEntityNotFoundException e)
                {
                    Logger.Warn(e, "Subscription {0} for topic {1} is not found (but was just found seconds earlier)", subscription, topic);
                }
            }
            else
            {
                try
                {
                    await RetryPolicy.ExecuteAsync(() => _nm.CreateSubscriptionAsync(sd));
                    return;
                }
                catch (MessagingEntityAlreadyExistsException e)
                {
                    Logger.Trace(e, "Subscription {0} for topic {1} already exists", subscription, topic);
                }
            }

            // wtf
            await CreateSubscription(topic, subscription);
        }

        private async Task CreateMessagingEntity(string path)
        {
            Logger.Trace("Creating entity for path {0}", path);

            var pathParts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // are we a queue or a subscription?
            if (pathParts.Length == 1)
            {
                // we are a queue
                await CreateQueue(pathParts[0]);
            }
            else if (pathParts.Length == 3 && pathParts[1].Equals("subscriptions", StringComparison.OrdinalIgnoreCase))
            {
                // we are a subscription
                var topicName = pathParts[0];
                var subscriptionName = pathParts[2];
                await CreateTopic(topicName);
                await CreateSubscription(topicName, subscriptionName);
            }
            else
                throw new ArgumentException("Unsupported path format", "path");
        }

        private async Task<Exception> HandleMessage(object message, Envelope envelope, IEnumerable<Func<object, Envelope, Task>> handlers, CancellationToken token)
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
                    Logger.Warn(ex, "A consumer exception occurred handling message `{0:n}'", envelope.MessageId);
                return ae;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "A consumer exception occurred handling message `{0:n}'", envelope.MessageId);
                return ex;
            }

            Logger.Debug("END: Handled message with id `{0:n}'", envelope.MessageId);

            return null;
        }

        private readonly ConcurrentMultiMap<string, CancellationTokenSource> _pathToReceiverCancelSources = new ConcurrentMultiMap<string, CancellationTokenSource>();
        private readonly ConcurrentMultiMap<string, Func<object, Envelope, Task>> _pathToHandlers = new ConcurrentMultiMap<string, Func<object, Envelope, Task>>();

        private async Task ReceiveMessages(string path, CancellationToken token)
        {
            MessageReceiver receiver = null;

            var registration = token.Register(() =>
            {
                try
                {
                    receiver.Close();
                }
                catch
                {
                }
            });

            bool recreate = false;

            try
            {
                receiver = await RetryPolicy.ExecuteAsync(() => _mf.CreateMessageReceiverAsync(path), token);

                while (!token.IsCancellationRequested)
                {
                    using (var brokeredMessage = await RetryPolicy.ExecuteAsync(() => receiver.ReceiveAsync(TimeSpan.FromMinutes(5)), token))
                    {
                        if (brokeredMessage == null) continue; // no message here

                        var stopwatch = Stopwatch.StartNew();

                        if (brokeredMessage.ContentType == null)
                        {
                            Logger.Warn("Encountered message {0} with null content type", brokeredMessage.MessageId);
                            await RetryPolicy.ExecuteAsync(() => brokeredMessage.DeadLetterAsync(), token);
                            continue;
                        }

                        var type = Type.GetType(brokeredMessage.ContentType, false);
                        if (type == null)
                        {
                            Logger.Warn("Encountered message {0} with {1} content type, which has no matching class", brokeredMessage.MessageId, brokeredMessage.ContentType);
                            await RetryPolicy.ExecuteAsync(() => brokeredMessage.DeadLetterAsync(), token);
                            continue;
                        }

                        Stream stream;
                        try
                        {
                            stream = brokeredMessage.GetBody<Stream>();
                        }
                        catch (Exception e)
                        {
                            Logger.Warn(e, "Could not turn brokered message with id {0} into a stream", brokeredMessage.MessageId);
                            continue;
                        }

                        object message;
                        try
                        {
                            message = _settings.Serializer.Deserialize(type, stream);
                        }
                        catch (Exception e)
                        {
                            Logger.Warn(e, "Serializer exception while deserializing message with id {0}", brokeredMessage.MessageId);
                            continue;
                        }
                        finally
                        {
                            if (stream != null)
                                stream.Dispose();
                        }

                        var envelope = new Envelope
                        {
                            MessageId = Guid.Parse(brokeredMessage.MessageId),
                            SequenceNumber = brokeredMessage.SequenceNumber
                        };

                        using (var lockLostCts = new CancellationTokenSource())
                        using (var renewalCts = new CancellationTokenSource())
                        {
                            bool doneProcessing = false;

                            Task.Run(async () =>
                            {
                                var renewalToken = renewalCts.Token;

                                while (true)
                                {
                                    renewalToken.ThrowIfCancellationRequested();

                                    var timeToWait = brokeredMessage.LockedUntilUtc - DateTime.UtcNow;
                                    timeToWait = new TimeSpan(timeToWait.Ticks - (long)(timeToWait.Ticks * _settings.RenewalThreshold)); // add in a cushion
                                    if (timeToWait > TimeSpan.Zero) await Task.Delay(timeToWait, renewalToken);

                                    try
                                    {
                                        Logger.Info("Renewing lock on message {0} of type {1} due to long consumer runtime. Consider increasing lock duration", brokeredMessage.MessageId, type);
                                        await RetryPolicy.ExecuteAsync(() => brokeredMessage.RenewLockAsync(), renewalToken);
                                    }
                                    catch (MessageLockLostException)
                                    {
                                        if (!renewalToken.IsCancellationRequested && !doneProcessing)
                                        {
                                            Logger.Warn("Failed to renew lock on message of type {0}, after {1} processing time. The message will be available again for dequeueing", type, stopwatch.Elapsed);
                                            lockLostCts.Cancel();
                                        }

                                        return;
                                    }
                                }
                            }, renewalCts.Token);

                            var ex = await HandleMessage(message, envelope, _pathToHandlers[path], lockLostCts.Token);
                            doneProcessing = true;

                            try
                            {
                                if (ex == null) await RetryPolicy.ExecuteAsync(() => brokeredMessage.CompleteAsync(), token);
                                else await RetryPolicy.ExecuteAsync(() => brokeredMessage.DeadLetterAsync("A consumer exception occurred", ex.ToString()), token);
                            }
                            catch (MessageLockLostException e)
                            {
                                Logger.Warn(e, "Lost lock on message of type {0}, after {1} processing time. The message will be available again for dequeueing", type, stopwatch.Elapsed);
                            }

                            renewalCts.Cancel(); // stop renewing
                        }
                    }
                }
                
                throw new OperationCanceledException(token);
            }
            catch (MessagingEntityNotFoundException e)
            {
                recreate = true;
                Logger.Warn(e, "Receiver for path {0} shut down because the messaging entity was deleted.", path);
            }
            catch (OperationCanceledException e)
            {
                if (e.CancellationToken == token)
                {
                    Logger.Debug("Receiver for path {0} shut down gracefully", path);
                    throw new OperationCanceledException(token);
                }
                else
                    Logger.Fatal(e, "Receiver for path {0} shut down due to unhandled exception in the message pump; this indicates a bug in PushoverQ", path);
            }
            catch (Exception e)
            {
                Logger.Fatal(e, "Receiver for path {0} shut down due to unhandled exception in the message pump; this indicates a bug in PushoverQ", path);
            }
            finally
            {
                registration.Dispose();
            }

            try
            {
                await RetryPolicy.ExecuteAsync(() => receiver.CloseAsync());
            }
            catch
            {
                // don't care
            }

            if (recreate)
                await CreateMessagingEntity(path);
        }

        private async void SpinUpReceiver(string path)
        {
            var cts = new CancellationTokenSource();
            var token = cts.Token;
            _pathToReceiverCancelSources.Add(path, cts);

            // keep trying to receive until we're told to stop
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await ReceiveMessages(path, token);
                }
                catch (OperationCanceledException e)
                {
                    if (e.CancellationToken != token) throw;
                }
            }

            try
            {
                cts.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // don't care
            }

            _pathToReceiverCancelSources.Remove(path, cts); // silently fails if missing
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

            var path = SubscriptionClient.FormatSubscriptionPath(topic, subscription);
            return await Subscribe(path, handler);
        }

        /// <inheritdoc/>
        public async Task<ISubscription> Subscribe(string path, Func<object, Envelope, Task> handler)
        {
            Logger.Info("Subscribing to path: `{0}'", path);

            await CreateMessagingEntity(path);

            for (var i = _pathToReceiverCancelSources.CountValues(path); i < _settings.NumberOfReceiversPerSubscription; i++)
                SpinUpReceiver(path);

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
                var msg = m as RPC.MethodCallCommand;
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
            if (_disposed) return;

            foreach (var cts in _pathToReceiverCancelSources.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }

            SpinWait.SpinUntil(() => _pathToReceiverCancelSources.Count == 0);

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
