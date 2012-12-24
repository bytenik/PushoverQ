using System;

namespace PushoverQ
{
    public interface IContext
    {
        bool NeedsConfirmation { get; }
        DateTime Expiration { get; }
        DateTime EnqueueAfter { get; }
    }
}