using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PushoverQ
{
    public interface IBus
    {
        Task Publish(object message);
        Task Publish(object message, bool confirm);

        Task<T> Publish<T>(object message);
        Task<T> Publish<T>(object message, TimeSpan timeout);
        Task<T> Publish<T>(object message, CancellationToken token);
        Task<T> Publish<T>(object message, TimeSpan timeout, CancellationToken token);
    }
}
