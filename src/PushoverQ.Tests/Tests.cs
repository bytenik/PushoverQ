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
            var evt = new ManualResetEventSlim();
            await _testBus.Subscribe<string>(async m => evt.Set());
            await _testBus.Send("testing");
            evt.Wait();
            Thread.Sleep(5000);
        }

        /// <summary>
        /// Publish a 1KB message and ensure that the message is received back.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        [Test]
        public async Task Publish1KBMessage()
        {
            const int MessageSize = 1 * 1024;
            Console.WriteLine("Message size: {0} bytes.", MessageSize);
            var consumer = _testBus.Consume<byte[]>();

            await TimedPublishByte(MessageSize);

            var result = await consumer;
            Assert.IsInstanceOf<byte[]>(result);
            Assert.IsTrue(result.Length == MessageSize);
        }

        /// <summary>
        /// Publish a 10KB message and ensure that the message is received back.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        [Test]
        public async Task Publish10KBMessage()
        {
            const int MessageSize = 10 * 1024;
            Console.WriteLine("Message size: {0} bytes.", MessageSize);
            var consumer = _testBus.Consume<byte[]>();

            await TimedPublishByte(MessageSize);

            var result = await consumer;
            Assert.IsInstanceOf<byte[]>(result);
            Assert.IsTrue(result.Length == MessageSize);
        }

        /// <summary>
        /// Publish a 1MB message and ensure that the message is received back.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        [Test]
        public async Task Publish1MBMessage()
        {
            const int MessageSize = 1 * 1024 * 1024;
            Console.WriteLine("Message size: {0} bytes.", MessageSize);
            var consumer = _testBus.Consume<byte[]>();

            await TimedPublishByte(MessageSize);

            var result = await consumer;
            Assert.IsInstanceOf<byte[]>(result);
            Assert.IsTrue(result.Length == MessageSize);
        }

        /// <summary>
        /// Publish a 10MB message and ensure that the message is received back.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        [Test]
        public async Task Publish10MBMessage()
        {
            const int MessageSize = 10 * 1024 * 1024;
            Console.WriteLine("Message size: {0} bytes.", MessageSize);
            var consumer = _testBus.Consume<byte[]>();

            await TimedPublishByte(MessageSize);

            var result = await consumer;
            Assert.IsInstanceOf<byte[]>(result);
            Assert.IsTrue(result.Length == MessageSize);
        }

        /// <summary>
        /// Publish 5000 messages and ensure that the messages are received back.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        [Test]
        public async Task Publish5000StringMessages()
        {
            const int MessageCountToPublish = 5000;
            var resultList = new List<string>();
            await _testBus.Subscribe((string s) =>
            {
                resultList.Add(s);
                return null;
            });

            await TimedPublishManyString(MessageCountToPublish);

            SpinWait.SpinUntil(() => resultList.Count == MessageCountToPublish);

            Assert.IsTrue(resultList.Count == MessageCountToPublish);
            for (int i = 0; i < MessageCountToPublish; i++)
                Assert.Contains(i.ToString(CultureInfo.InvariantCulture), resultList);
        }

        /// <summary>
        /// Publish 5000 messages and ensure that the messages are received back.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        [Test]
        public async Task Publish5000OneKBMessages()
        {
            const int MessageCountToPublish = 5000;
            const int MessageSizeToPublish = 1024;
            var resultList = new List<byte[]>();
            await _testBus.Subscribe((byte[] s) =>
            {
                resultList.Add(s);
                return null;
            });

            await TimedPublishByte(MessageSizeToPublish, MessageCountToPublish);

            SpinWait.SpinUntil(() => resultList.Count == MessageCountToPublish);

            Assert.IsTrue(resultList.Count == MessageCountToPublish);
            Assert.IsTrue(resultList.TrueForAll(x => x.Length == MessageSizeToPublish));
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

        private async Task TimedPublishByte(int size)
        {
            await TimedPublishByte(size, 1);
        }

        private async Task TimedPublishByte(int size, int count)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
                await _testBus.Send(new byte[size]);
            sw.Stop();
            Console.WriteLine("Message Publish took {0}", sw.Elapsed);
        }

        private async Task TimedPublishManyString(int count)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
                await _testBus.Send(i.ToString(CultureInfo.InvariantCulture));

            sw.Stop();
            Console.WriteLine("Message Publish took {0}", sw.Elapsed);
        }
    }
}
