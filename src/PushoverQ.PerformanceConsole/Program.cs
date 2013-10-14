using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ.PerformanceConsole
{
    /// <summary>
    /// The program.
    /// </summary>
    public class Program
    {
        static void Main(string[] args)
        {
            using (var large = new LargePublishPerformance())
            {
                large.Run().Wait();
            }

            using (var small = new SmallPublishPerformance())
            {
                small.Run().Wait();
            }
        }
    }
}
