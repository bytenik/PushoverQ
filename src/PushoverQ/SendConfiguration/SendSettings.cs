using System;

namespace PushoverQ.ContextConfiguration
{
    public class SendSettings : ISendSettings
    {
        public bool NeedsConfirmation { get; internal set; }
        public TimeSpan? Expiration { get; internal set; }
        public DateTime? VisibleAfter { get; internal set; }
        public string Topic { get; internal set; }
    }
}
