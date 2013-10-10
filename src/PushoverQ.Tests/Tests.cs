using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using Microsoft.ServiceBus;
using NLog;
using NLog.Config;
using NUnit.Framework;

namespace PushoverQ.Tests
{
    using System.Threading.Tasks;

    [TestFixture]
    public class Tests
    {
        [Test]
        public async Task BringUpBus()
        {
            var serverFQDN = Environment.MachineName;
            const int HttpPort = 9355;
            const int TcpPort = 9354;
            const string ServiceNamespace = "ServiceBusDefaultNamespace";

            Common.Logging.LogManager.Adapter = new Common.Logging.NLog.NLogLoggerFactoryAdapter(new NameValueCollection { { "configType", "FILE" }, {"configFile", "~/NLog.config" } } );

            var connBuilder = new ServiceBusConnectionStringBuilder { ManagementPort = HttpPort, RuntimePort = TcpPort };
            connBuilder.Endpoints.Add(new UriBuilder { Scheme = "sb", Host = serverFQDN, Path = ServiceNamespace }.Uri);
            connBuilder.StsEndpoints.Add(new UriBuilder { Scheme = "https", Host = serverFQDN, Port = HttpPort, Path = ServiceNamespace }.Uri);
            var bus = Bus.CreateBus(cfg => cfg.WithConnectionString(connBuilder.ToString())).Result;

            var evt = new ManualResetEventSlim();
            await bus.Subscribe<string>(async m => evt.Set());
            await bus.Send("testing");
            evt.Wait();
            Thread.Sleep(5000);
        }
    }
}
