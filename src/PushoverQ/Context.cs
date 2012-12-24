using System;

namespace PushoverQ
{
    public class Context : IContext
    {
        public bool NeedsConfirmation { get; internal set; }
        public DateTime Expiration { get; internal set; }
        public DateTime EnqueueAfter { get; internal set; }
    }
}
