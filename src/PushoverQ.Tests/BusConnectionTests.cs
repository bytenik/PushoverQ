using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.ServiceBus;

using NUnit.Framework;

namespace PushoverQ.Tests
{
    using System.Threading.Tasks;

    /// <summary>
    /// The bus connection tests.
    /// </summary>
    [TestFixture]
    public class BusConnectionTests : IDisposable
    {
        /// <summary>
        /// The test bus.
        /// </summary>
        IBus _testBus;

        /// <summary>
        /// Setup the bus for all the subsequent tests.
        /// </summary>
        [TestFixtureSetUp]
        public void SetUp()
        {
            var serverFQDN = Environment.MachineName;
            const int HttpPort = 9355;
            const int TcpPort = 9354;
            const string ServiceNamespace = "ServiceBusDefaultNamespace";

            // Common.Logging.LogManager.Adapter = new Common.Logging.NLog.NLogLoggerFactoryAdapter(new NameValueCollection { { "configType", "FILE" }, { "configFile", "~/NLog.config" } });

            var connBuilder = new ServiceBusConnectionStringBuilder { ManagementPort = HttpPort, RuntimePort = TcpPort };
            connBuilder.Endpoints.Add(new UriBuilder { Scheme = "sb", Host = serverFQDN, Path = ServiceNamespace }.Uri);
            connBuilder.StsEndpoints.Add(new UriBuilder { Scheme = "https", Host = serverFQDN, Port = HttpPort, Path = ServiceNamespace }.Uri);
            _testBus = Bus.CreateBus(cfg => cfg.WithConnectionString(connBuilder.ToString())).Result;
        }

        /// <summary>
        /// Bring up the bus and send a test message.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        [Test]
        public async Task BringUpBus()
        {
            var consumer = _testBus.Consume<string>();
            await _testBus.Send("testing");
            var result = await consumer;
            Assert.NotNull(result, "Response cannot be null.");
            Assert.IsTrue(result == "testing", "Response content is not what was sent to bus.");
        }

        [Test]
        public async Task DisconnectBuDuringPublish()
        {
            var consumer = _testBus.Consume<string>();
            await _testBus.Send("testing");
            
        }

        /// <summary>
        /// The tear down.
        /// </summary>
        [TestFixtureTearDown]
        public void TearDown()
        {
            _testBus.Dispose();
        }

        /// <summary>
        /// The dispose.
        /// </summary>
        public void Dispose()
        {
            _testBus.Dispose();
        }

        ~BusConnectionTests()
        {
            _testBus.Dispose();
        }
    }
}
