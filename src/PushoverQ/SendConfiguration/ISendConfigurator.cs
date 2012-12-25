using System;

namespace PushoverQ.SendConfiguration
{
    public interface ISendConfigurator
    {
        void ToTopic(string name);
        void WithPublisherConfirms();
        void ExpiresAfter(TimeSpan expiration);
        void EnqueueAfter(DateTime enqueueAfter);
    }
}