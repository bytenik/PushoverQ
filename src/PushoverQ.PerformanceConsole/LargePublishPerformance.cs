using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.ServiceBus;

namespace PushoverQ.PerformanceConsole
{
    /// <summary>
    /// Test the performance of publishing a few larger messages.
    /// </summary>
    internal class LargePublishPerformance : IPublishPerformance
    {
       private readonly IBus _bus;

        /// <summary>
        /// Initializes a new instance of the <see cref="LargePublishPerformance"/> class.
        /// </summary>
        public LargePublishPerformance()
        {
            var serverFQDN = Environment.MachineName;
            const int HttpPort = 9355;
            const int TcpPort = 9354;
            const string ServiceNamespace = "ServiceBusDefaultNamespace";

            // Common.Logging.LogManager.Adapter = new Common.Logging.NLog.NLogLoggerFactoryAdapter(new NameValueCollection { { "configType", "FILE" }, { "configFile", "~/NLog.config" } });

            var connBuilder = new ServiceBusConnectionStringBuilder { ManagementPort = HttpPort, RuntimePort = TcpPort };
            connBuilder.Endpoints.Add(new UriBuilder { Scheme = "sb", Host = serverFQDN, Path = ServiceNamespace }.Uri);
            connBuilder.StsEndpoints.Add(new UriBuilder { Scheme = "https", Host = serverFQDN, Port = HttpPort, Path = ServiceNamespace }.Uri);
            _bus = Bus.CreateBus(cfg => cfg.WithConnectionString(connBuilder.ToString())).Result;
        }

        /// <summary>
        /// Run the test by publishing 100 byte arrays of varying size, at least 1KB, at most 512KB.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public async Task Run()
        {
            var resultList = new List<byte[]>();
            await _bus.Subscribe((byte[] s) =>
            {
                resultList.Add(s);
                return null;
            });

            var random = new Random(524288);
            for (int i = 0; i < 10000; i++)
            {
                await _bus.Send(new byte[random.Next(1024, 254 * 1024)]);
            }

            SpinWait.SpinUntil(() => resultList.Count == 100);
        }

        /// <summary>
        /// Dispose the Performance Test by calling dispose on the bus.
        /// </summary>
        public void Dispose()
        {
            _bus.Dispose();
        }

        ~LargePublishPerformance()
        {
            _bus.Dispose();
        }
    }
}
