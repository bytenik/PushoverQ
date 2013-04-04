using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ
{
    /// <summary>
    /// A subscription.
    /// </summary>
    public interface ISubscription : IDisposable
    {
        /// <summary>
        /// Unsubscribe from the subscription.
        /// </summary>
        /// <returns> The <see cref="Task"/> that is completed once the process occurs. </returns>
        Task Unsubscribe();
    }
}
