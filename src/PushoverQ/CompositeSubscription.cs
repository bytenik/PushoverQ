﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;

namespace PushoverQ
{
    /// <summary>
    /// A composite subscription.
    /// </summary>
    public class CompositeSubscription : ISubscription
    {
        private readonly ISubscription[] _subscriptions;
        private bool _disposed;

        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeSubscription"/> class.
        /// </summary>
        /// <param name="subscriptions"> The subscriptions. </param>
        /// <exception cref="ArgumentNullException"> Thrown when subscriptions is null.</exception>
        /// <exception cref="NullReferenceException"> Thrown when a particular subscription is null. </exception>
        public CompositeSubscription(params ISubscription[] subscriptions)
        {
            if (subscriptions == null) throw new ArgumentNullException("subscriptions");
            if (subscriptions.Any(x => x == null)) throw new NullReferenceException("A subscription is null.");

            _subscriptions = subscriptions;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeSubscription"/> class.
        /// </summary>
        /// <param name="subscriptions"> The subscriptions. </param>
        /// <exception cref="ArgumentNullException"> Thrown when subscriptions is null.</exception>
        /// <exception cref="NullReferenceException"> Thrown when a particular subscription is null. </exception>
        public CompositeSubscription(IEnumerable<ISubscription> subscriptions)
            : this(subscriptions == null ? null : subscriptions as ISubscription[] ?? subscriptions.ToArray())
        {
        }

        public void Dispose()
        {
            if (_disposed) throw new ObjectDisposedException(typeof(CompositeSubscription).Name);
            _disposed = true;

            foreach (var subscription in _subscriptions) subscription.Dispose();
        }

        public Task Unsubscribe()
        {
            if (_disposed) return Task.FromResult<object>(null);
            _disposed = true;
            
            return Task.WhenAll(_subscriptions.Select(x => x.Unsubscribe()));
        }

        ~CompositeSubscription()
        {
            if (!_disposed) Log.Info("Finalizer reached a subscription that was not unsubscribed or disposed.");
        }
    }
}
