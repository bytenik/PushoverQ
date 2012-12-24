using System;

namespace PushoverQ.ContextConfiguration
{
    public interface IContextConfigurator
    {
        void WithPublisherConfirms();
        void ExpiresAfter(DateTime expiration);
        void EnqueueAfter(DateTime enqueueAfter);
    }

    public static class ContextConfiguratorExtensions
    {
        public static void ExpiresAfter(this IContextConfigurator configurator, DateTimeOffset expiration)
        {
            configurator.ExpiresAfter(expiration.UtcDateTime);
        }

        public static void EnqueueAfter(this IContextConfigurator configurator, DateTimeOffset enqueueAfter)
        {
            configurator.EnqueueAfter(enqueueAfter.UtcDateTime);
        }
    }
}