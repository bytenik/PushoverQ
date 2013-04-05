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
        /// Sends a message to a single destination.
        /// </summary>
        /// <param name="message"> The message. </param>
        /// <param name="destination"> The destination topic or endpoint, or null for a competing destination. </param>
        /// <param name="confirmation"> true if a confirmation reply is needed; false otherwise. </param>
        /// <param name="expiration"> <see cref="DateTime"/> after which the message should no longer be available for receipt. </param>
        /// <param name="visibleAfter"> <see cref="DateTime"/> before which the message should not be available for receipt. </param>
        /// <param name="timeout">
        /// The <see cref="TimeSpan"/> after which the send is aborted. If <see cref="confirmation"/> is not false,
        /// <see cref="timeout"/> also includes the time for a reply message to be received. Specify null for no timeout.
        /// </param>
        /// <param name="token"> A cancellation token to monitor. Canceling the token will abort the send, or
        /// if <see cref="confirmation" /> is not false, will abort waiting for a confirmation reply. </param>
        /// <returns> The <see cref="Task"/> that completes upon successfully queuing of the message, or
        /// <see cref="confirmation"/> is true, completes upon successfully receiving a confirmation reply. </returns>
        Task Send(object message,
            string destination = null,
            bool confirmation = false,
            TimeSpan? expiration = null,
            DateTime? visibleAfter = null,
            TimeSpan? timeout = null,
            CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Sends a message to a single destination, and waits for a reply of type <see cref="T"/>.
        /// </summary>
        /// <param name="message"> The message. </param>
        /// <param name="destination"> The destination topic or endpoint, or null for a competing destination. </param>
        /// <param name="expiration"> <see cref="DateTime"/> after which the message should no longer be available for receipt. </param>
        /// <param name="visibleAfter"> <see cref="DateTime"/> before which the message should not be available for receipt. </param>
        /// <param name="timeout">
        /// The <see cref="TimeSpan"/> after which the send is aborted. This also includes the time for a reply message
        /// to be received. Specify null for no timeout.
        /// </param>
        /// <param name="token"> A cancellation token to monitor. Canceling the token will abort sending or waiting for a reply. </param>
        /// <typeparam name="T"> The type of expected reply. </typeparam>
        /// <returns> The <see cref="Task{T}"/> that completes upon successfully receiving a confirmation reply. </returns>
        Task<T> Send<T>(object message,
            string destination = null,
            TimeSpan? expiration = null,
            DateTime? visibleAfter = null,
            TimeSpan? timeout = null,
            CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Publishes a message to all subscribed endpoints.
        /// </summary>
        /// <param name="message"> The message. </param>
        /// <param name="expiration"> <see cref="DateTime"/> after which the message should no longer be available for receipt. </param>
        /// <param name="visibleAfter"> <see cref="DateTime"/> before which the message should not be available for receipt. </param>
        /// <param name="timeout"> The <see cref="TimeSpan"/> after which the send is aborted. </param>
        /// <param name="token"> A cancellation token to monitor. Canceling the token will abort the send. </param>
        /// <returns> The <see cref="Task"/> that completes upon successfully queuing of the message. </returns>
        Task Publish(object message,
            TimeSpan? expiration = null,
            DateTime? visibleAfter = null,
            TimeSpan? timeout = null,
            CancellationToken token = default(CancellationToken));

        Task<ISubscription> Subscribe(Type type, Func<object, Envelope, Task> handler);
        Task<ISubscription> Subscribe<T>(Func<T, Envelope, Task> handler) where T : class;

        Task<ISubscription> Subscribe(string topic, string subscription, Func<object, Envelope, Task> handler);

        T GetProxy<T>();
        Task<ISubscription> Subscribe<T>(Func<T> resolver);
    }
}
