using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Threading;
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

            Common.Logging.LogManager.Adapter = new Common.Logging.NLog.NLogLoggerFactoryAdapter(new NameValueCollection { { "configType", "FILE" }, { "configFile", "~/NLog.config" } });

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
            var evt = new ManualResetEventSlim();
            await _testBus.Subscribe<string>(async m => evt.Set());
            await _testBus.Send("testing");
            evt.Wait();
            Thread.Sleep(5000);
        }

        /// <summary>
        /// Publish a large message and ensure that the message is received back.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        [Test]
        public async Task PublishLargeMessage()
        {
            const int MessageSize = short.MaxValue - 1;
            Console.WriteLine("Message size: {0} bytes.", MessageSize);
            var consumer = _testBus.Consume<byte[]>();

            var sw = Stopwatch.StartNew();
            Console.WriteLine("Publishing message");
            await _testBus.Send(new byte[MessageSize]);
            sw.Stop();
            Console.WriteLine("Message Publish took {0}", sw.Elapsed);

            var result = await consumer;
            Assert.IsInstanceOf<byte[]>(result);
            Assert.IsTrue(result.Length == MessageSize);
        }

        /// <summary>
        /// Publish a large message and ensure that the message is received back.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        [Test]
        public async Task Publish5000Messages()
        {
            const int MessageCountToPublish = 50;
            var resultList = new List<string>();
            await _testBus.Subscribe((string s) =>
            {
                resultList.Add(s);
                return null;
            });

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MessageCountToPublish; i++)
            {
                await _testBus.Send(i.ToString(CultureInfo.InvariantCulture));
            }

            sw.Stop();
            Console.WriteLine("Message Publish took {0}", sw.Elapsed);

            SpinWait.SpinUntil(() => resultList.Count == MessageCountToPublish);
            Assert.IsTrue(resultList.Count == MessageCountToPublish);
            for (int i = 0; i < MessageCountToPublish; i++)
            {
                Assert.Contains(i.ToString(CultureInfo.InvariantCulture), resultList);
            }
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

    }
}
