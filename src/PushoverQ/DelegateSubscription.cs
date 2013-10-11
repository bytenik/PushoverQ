using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ
{
    class DelegateSubscription : ISubscription
    {
        private readonly Func<Task> _unsubscribe;

        public DelegateSubscription(Func<Task> unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
        }

        public Task Unsubscribe()
        {
            return _unsubscribe();
        }
    }
}
