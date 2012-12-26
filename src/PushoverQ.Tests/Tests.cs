using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.ServiceBus;
using NLog;
using NLog.Config;
using NUnit.Framework;

namespace PushoverQ.Tests
{
    [TestFixture]
    public class Tests
    {
        [Test]
        public void BringUpBus()
        {
            string ServerFQDN = Environment.MachineName;
            int HttpPort = 9355;
            int TcpPort = 9354;
            string ServiceNamespace = "ServiceBusDefaultNamespace";

            Common.Logging.LogManager.Adapter = new Common.Logging.NLog.NLogLoggerFactoryAdapter(new NameValueCollection
                                                                                                     {
                                                                                                         {"configType", "FILE"},
                                                                                                         {"configFile", "~/NLog.config"}
                                                                                                     });

            ServiceBusConnectionStringBuilder connBuilder = new ServiceBusConnectionStringBuilder();
            connBuilder.ManagementPort = HttpPort;
            connBuilder.RuntimePort = TcpPort;
            connBuilder.Endpoints.Add(new UriBuilder() { Scheme = "sb", Host = ServerFQDN, Path = ServiceNamespace }.Uri);
            connBuilder.StsEndpoints.Add(new UriBuilder() { Scheme = "https", Host = ServerFQDN, Port = HttpPort, Path = ServiceNamespace }.Uri);
            IBus bus = Bus.CreateBus(cfg =>
                                         {
                                             cfg.WithConnectionString(connBuilder.ToString());
                                         }).Result;

            bool handled = false;
            bus.Subscribe<string>(async m => handled = true).Wait();
            bus.Publish("testing").Wait();
            Thread.Sleep(5000);
            Assert.True(handled);
        }
    }
}
