using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PushoverQ
{
    /// <summary>
    /// The bus interface.
    /// </summary>
    public interface IBus : IDisposable
    {
        /// <summary>
        /// Sends a message.
        /// </summary>
        /// <param name="message"> The message. </param>
        /// <param name="destination"> The destination topic or endpoint, or null for the default (conventions-based) destination. </param>
        /// <param name="confirmation"> true if a confirmation reply is needed; false otherwise. </param>
        /// <param name="expiration"> <see cref="DateTime"/> after which the message should no longer be available for receipt. </param>
        /// <param name="visibleAfter"> <see cref="DateTime"/> before which the message should not be available for receipt. </param>
        /// <param name="token"> A cancellation token to monitor. Canceling the token will abort the send, or
        /// if <see cref="confirmation" /> is not false, will abort waiting for a confirmation reply. </param>
        /// <returns> The <see cref="Task"/> that completes upon successfully queuing of the message, or
        /// <see cref="confirmation"/> is true, completes upon successfully receiving a confirmation reply. </returns>
        Task Send(object message,
            string destination = null,
            bool confirmation = false,
            TimeSpan? expiration = null,
            DateTime? visibleAfter = null,
            CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Sends a message.
        /// </summary>
        /// <param name="messages"> The messages. </param>
        /// <param name="destination"> The destination topic or endpoint, or null for the default (conventions-based) destination. </param>
        /// <param name="confirmation"> true if a confirmation reply is needed; false otherwise. </param>
        /// <param name="expiration"> <see cref="DateTime"/> after which the message should no longer be available for receipt. </param>
        /// <param name="visibleAfter"> <see cref="DateTime"/> before which the message should not be available for receipt. </param>
        /// <param name="token"> A cancellation token to monitor. Canceling the token will abort the send, or
        /// if <see cref="confirmation" /> is not false, will abort waiting for a confirmation reply. </param>
        /// <returns> The <see cref="Task"/> that completes upon successfully queuing of the message, or
        /// <see cref="confirmation"/> is true, completes upon successfully receiving a confirmation reply. </returns>
        Task Send(IEnumerable<object> messages,
            string destination = null,
            bool confirmation = false,
            TimeSpan? expiration = null,
            DateTime? visibleAfter = null,
            CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Sends a message, and waits for a single reply of messageType <see cref="T"/>.
        /// </summary>
        /// <param name="message"> The message. </param>
        /// <param name="destination"> The destination topic or endpoint, or null for the default (conventions-based) destination. </param>
        /// <param name="expiration"> <see cref="DateTime"/> after which the message should no longer be available for receipt. </param>
        /// <param name="visibleAfter"> <see cref="DateTime"/> before which the message should not be available for receipt. </param>
        /// <param name="token"> A cancellation token to monitor. Canceling the token will abort sending or waiting for a reply. </param>
        /// <typeparam name="T"> The messageType of expected reply. </typeparam>
        /// <returns> The <see cref="Task{T}"/> that completes upon successfully receiving a confirmation reply. </returns>
        Task<T> Send<T>(object message,
            string destination = null,
            TimeSpan? expiration = null,
            DateTime? visibleAfter = null,
            CancellationToken token = default(CancellationToken));

        Task<ISubscription> Subscribe(Type messageType, Func<object, Envelope, Task> handler);

        Task<ISubscription> Subscribe(Type messageType, string subscription, Func<object, Envelope, Task> handler);

        Task<ISubscription> Subscribe(string topic, string subscription, Func<object, Envelope, Task> handler);

        Task<ISubscription> Subscribe<T>(Func<T, Envelope, Task> handler) where T : class;

        Task<ISubscription> Subscribe<T>(string subscription, Func<T, Envelope, Task> handler) where T : class;

        Task<ISubscription> Subscribe<T>(string topic, string subscription, Func<T, Envelope, Task> handler) where T : class;

        T GetProxy<T>();

        Task<ISubscription> Subscribe<T>(Func<T> resolver);

        ILog Logger { get; }
    }
}