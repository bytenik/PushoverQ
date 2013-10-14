using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.ServiceBus;

namespace PushoverQ.PerformanceConsole
{
    /// <summary>
    /// Test the performance of publishing a lot of small messages.
    /// </summary>
    internal class SmallPublishPerformance : IPublishPerformance
    {
        private readonly IBus _bus;

        /// <summary>
        /// Initializes a new instance of the <see cref="SmallPublishPerformance"/> class.
        /// </summary>
        public SmallPublishPerformance()
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
        /// Run the test by publishing 10000 strings of varying lengths and content, all under 50 bytes.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public async Task Run()
        {
            var resultList = new List<string>();
            await _bus.Subscribe((string s) =>
            {
                resultList.Add(s);
                return null;
            });

            for (int i = 0; i < 10000; i++)
            {
                await _bus.Send(string.Format("This is test string #{0}", i));
            }

            SpinWait.SpinUntil(() => resultList.Count == 10000);
        }

        /// <summary>
        /// Dispose the Performance Test by calling dispose on the bus.
        /// </summary>
        public void Dispose()
        {
            _bus.Dispose();
        }

        ~SmallPublishPerformance()
        {
            _bus.Dispose();
        }
    }
}
