using System;

namespace PushoverQ
{
    public interface ISendSettings
    {
        bool NeedsConfirmation { get; }
        TimeSpan? Expiration { get; }
        DateTime? VisibleAfter { get; }
    }
}