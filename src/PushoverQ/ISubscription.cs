using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ
{
    public interface ISubscription : IDisposable
    {
        Task DisposeAsync();
    }
}
