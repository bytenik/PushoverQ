using System;
using PushoverQ.ContextConfiguration;

namespace PushoverQ.SendConfiguration
{
    class SendConfigurator : ISendConfigurator
    {
        internal SendSettings SendSettings { get; private set; }

        internal SendConfigurator()
        {
            SendSettings = new SendSettings();
        }

        public void ToTopic(string name)
        {
            SendSettings.Topic = name;
        }

        public void WithPublisherConfirms()
        {
            SendSettings.NeedsConfirmation = true;
        }

        public void ExpiresAfter(TimeSpan expiration)
        {
            SendSettings.Expiration = expiration;
        }

        public void EnqueueAfter(DateTime enqueueAfter)
        {
            SendSettings.VisibleAfter = enqueueAfter;
        }
    }
}
