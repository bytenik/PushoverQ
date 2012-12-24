using System;

namespace PushoverQ.ContextConfiguration
{
    class ContextConfigurator : IContextConfigurator
    {
        internal Context Context { get; private set; }

        internal ContextConfigurator()
        {
            Context = new Context();
        }

        public void WithPublisherConfirms()
        {
            Context.NeedsConfirmation = true;
        }

        public void ExpiresAfter(DateTime expiration)
        {
            Context.Expiration = expiration;
        }

        public void EnqueueAfter(DateTime enqueueAfter)
        {
            Context.EnqueueAfter = enqueueAfter;
        }
    }
}
