using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.TransientFaultHandling;
using Microsoft.ServiceBus;

namespace PushoverQ
{
    class SubscriptionDisposer : ISubscription
    {
        private readonly NamespaceManager _namespaceManager;
        private readonly RetryPolicy _retryPolicy;
        private readonly string _topic;
        private readonly string _subscription;
        private readonly bool _isTransient;

        public SubscriptionDisposer(string topic, string subscription, bool isTransient, NamespaceManager namespaceManager, RetryPolicy retryPolicy)
        {
            _topic = topic;
            _subscription = subscription;
            _isTransient = isTransient;
            _namespaceManager = namespaceManager;
            _retryPolicy = retryPolicy;
        }

        public void Dispose(bool disposing)
        {
            if (disposing || _isTransient)
                _retryPolicy.ExecuteAction(() => _namespaceManager.DeleteSubscription(_topic, _subscription));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async Task DisposeAsync()
        {
            await _retryPolicy.ExecuteAsync(() => Task.Factory.FromAsync(_namespaceManager.BeginDeleteSubscription, _namespaceManager.EndDeleteSubscription, _topic, _subscription, null));
            GC.SuppressFinalize(this);
        }

        ~SubscriptionDisposer()
        {
            Dispose(false);
        }
    }
}
