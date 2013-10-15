using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ.PerformanceConsole
{
    /// <summary>
    /// The PublishPerformance interface.  This interface defines a standard set of methods for testing the performance of <see cref="PushoverQ"/>.
    /// </summary>
    internal interface IPublishPerformance : IDisposable
    {
        /// <summary>
        /// Run the test by publishing messages according to the implementation.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        Task Run();
    }
}
