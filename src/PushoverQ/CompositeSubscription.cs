using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ
{
    /// <summary>
    /// A composite subscription.
    /// </summary>
    public class CompositeSubscription : ISubscription
    {
        readonly ILog _logger;
        private readonly ISubscription[] _subscriptions;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeSubscription"/> class.
        /// </summary>
        /// <param name="logger"> The logger. </param>
        /// <param name="subscriptions"> The subscriptions. </param>
        /// <exception cref="ArgumentNullException"> Thrown when subscriptions is null.</exception>
        /// <exception cref="NullReferenceException"> Thrown when a particular subscription is null. </exception>
        public CompositeSubscription(ILog logger, params ISubscription[] subscriptions)
        {
            if (subscriptions == null) throw new ArgumentNullException("subscriptions");
            if (logger == null) throw new ArgumentNullException("bus");
            if (subscriptions.Any(x => x == null)) throw new NullReferenceException("A subscription is null.");

            _logger = logger;
            _subscriptions = subscriptions;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) throw new ObjectDisposedException(typeof(CompositeSubscription).Name);
            _disposed = true;

            foreach (var subscription in _subscriptions) subscription.Dispose();
        }

        /// <inheritdoc/>
        public Task Unsubscribe()
        {
            if (_disposed) return Task.FromResult<object>(null);
            _disposed = true;
            
            return Task.WhenAll(_subscriptions.Select(x => x.Unsubscribe()));
        }

        ~CompositeSubscription()
        {
            if (!_disposed) _logger.Info("Finalizer reached a subscription that was not unsubscribed or disposed.");
        }
    }
}
