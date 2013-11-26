using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.ServiceBus;

using NUnit.Framework;

namespace PushoverQ.Tests
{
    /// <summary>
    /// The publish tests.
    /// </summary>
    [TestFixture]
    public class PublishTests
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

            var connBuilder = new ServiceBusConnectionStringBuilder { ManagementPort = HttpPort, RuntimePort = TcpPort };
            connBuilder.OperationTimeout = TimeSpan.FromSeconds(10);
            connBuilder.Endpoints.Add(new UriBuilder { Scheme = "sb", Host = serverFQDN, Path = ServiceNamespace }.Uri);
            connBuilder.StsEndpoints.Add(new UriBuilder { Scheme = "https", Host = serverFQDN, Port = HttpPort, Path = ServiceNamespace }.Uri);
            _testBus = Bus.CreateBus(cfg => cfg.WithConnectionString(connBuilder.ToString()).WithLogger(new TestLogger())).Result;
            // _testBus = Bus.CreateBus(cfg => cfg.WithConnectionString(@"Endpoint=sb://funnelfire-test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ENUAYOiS9Lt3tDngyClvjzRyls5UkS8ie7aLAyjBV0s=;OperationTimeout=00:00:10").WithLogger(new TestLogger())).Result;
        }

        /// <summary>
        /// Publish a message and ensure that the message is renewed appropriately.
        /// </summary>
        /// <returns> The <see cref="Task"/>. </returns>
        [Test]
        public async Task SlowConsumerRenews()
        {
            bool gate = false;
            await _testBus.Subscribe<string>(async m =>
            {
                await Task.Delay(TimeSpan.FromMinutes(2));
                gate = true;
            });

            await _testBus.Send("test message");
            SpinWait.SpinUntil(() => gate);
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
            const int MessageCountToPublish = 1;
            const int MessageSize = 1 * 1024;
            var consumer = _testBus.Consume<byte[]>();

            var elapsed = await TimedPublishByte(MessageSize, MessageCountToPublish);
            var publishAverage = elapsed.TotalMilliseconds / MessageCountToPublish;
            Console.WriteLine("Time per message: {0} ms", publishAverage);

            var result = await consumer;
            Assert.NotNull(result, "Result cannot be null.");
            Assert.IsInstanceOf<byte[]>(result, "Result is not correct type.");
            Assert.IsTrue(result.Length == MessageSize, "Result size is incorrect.");
            Assert.IsTrue(publishAverage < 2000, "Publish took too long.");
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
            const int MessageCountToPublish = 1;
            const int MessageSize = 10 * 1024;
            var consumer = _testBus.Consume<byte[]>();

            var elapsed = await TimedPublishByte(MessageSize, MessageCountToPublish);
            var publishAverage = elapsed.TotalMilliseconds / MessageCountToPublish;
            Console.WriteLine("Time per message: {0} ms", publishAverage);

            var result = await consumer;
            Assert.NotNull(result, "Result cannot be null.");
            Assert.IsInstanceOf<byte[]>(result, "Result is not correct type.");
            Assert.IsTrue(result.Length == MessageSize, "Result size is incorrect.");
            Assert.IsTrue(publishAverage < 2000, "Publish took too long.");
        }

        /// <summary>
        /// Publish a 254KB message and ensure that the message is received back.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        [Test]
        public async Task Publish254KBMessage()
        {
            const int MessageCountToPublish = 1;
            const int MessageSize = 254 * 1024;

            var consumer = _testBus.Consume<byte[]>();

            var elapsed = await TimedPublishByte(MessageSize, MessageCountToPublish);
            var publishAverage = elapsed.TotalMilliseconds / MessageCountToPublish;
            Console.WriteLine("Time per message: {0} ms", publishAverage);

            var result = await consumer;
            Assert.NotNull(result, "Result cannot be null.");
            Assert.IsInstanceOf<byte[]>(result, "Result is not correct type.");
            Assert.IsTrue(result.Length == MessageSize, "Result size is incorrect.");
            Assert.IsTrue(publishAverage < 2000, "Publish took too long.");
        }

        /// <summary>
        /// Publish a 256KB message and ensure that the send fails.
        /// </summary>
        [Test]
        public void Publish256KBMessage()
        {
            const int MessageSize = 256 * 1024;

            var aggregateException = Assert.Throws<AggregateException>(() => _testBus.Send(new byte[MessageSize]).Wait(5000));
            Assert.IsTrue(aggregateException.InnerExceptions.Any(x => x is MessageSizeException));
        }

        /// <summary>
        /// Publish a 1MB message and ensure that the send fails.
        /// </summary>
        [Test]
        public void Publish1MBMessage()
        {
            const int MessageSize = 1024 * 1024;

            var aggregateException = Assert.Throws<AggregateException>(() => _testBus.Send(new byte[MessageSize]).Wait(5000));
            Assert.IsTrue(aggregateException.InnerExceptions.Any(x => x is MessageSizeException));
        }

        /// <summary>
        /// Publish a 10MB message and ensure that the send fails.
        /// </summary>
        [Test]
        public void Publish10MBMessage()
        {
            const int MessageSize = 1024 * 1024 * 10;

            var aggregateException = Assert.Throws<AggregateException>(() => _testBus.Send(new byte[MessageSize]).Wait(5000));
            Assert.IsTrue(aggregateException.InnerExceptions.Any(x => x is MessageSizeException));
        }

        /// <summary>
        /// Publish 500 messages and ensure that the messages are received back.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        [Test]
        public async Task Publish500NonCompressibleStringMessages()
        {
            const int MessageCountToPublish = 500;
            var resultList = new List<string>();
            await _testBus.Subscribe((string s) =>
            {
                resultList.Add(s);
                return null;
            });

            var randomBytes = new byte[10 * 1024];
            var random = new Random();
            random.NextBytes(randomBytes);
            
            var str = randomBytes.Aggregate(string.Empty, (current, randombyte) => current + (char)randombyte);
            var elapsed = await TimedPublishManyString(MessageCountToPublish, str);
            var publishAverage = elapsed.TotalMilliseconds / MessageCountToPublish;
            Console.WriteLine("Time per message: {0} ms", publishAverage);

            SpinWait.SpinUntil(() => resultList.Count == MessageCountToPublish);

            Assert.IsTrue(resultList.Count == MessageCountToPublish);
            Assert.IsTrue(resultList.TrueForAll(x => x == string.Empty), "Result does not contain expected result {0}", string.Empty);
            Assert.IsTrue(publishAverage < 50, "Publish took too long.");
        }

        /// <summary>
        /// Publish 500 messages and ensure that the messages are received back.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        [Test]
        public async Task Publish500CompressibleStringMessages()
        {
            const int StrLength = (200 * 1024) / 8;
            const int MessageCountToPublish = 500;
            var resultList = new List<string>();
            await _testBus.Subscribe((string s) =>
            {
                resultList.Add(s);
                return null;
            });

            var str = Enumerable.Range(0, StrLength).Aggregate(string.Empty, (s, i) => s += "abcdefg ");

            var elapsed = await TimedPublishManyString(MessageCountToPublish, str);
            var publishAverage = elapsed.TotalMilliseconds / MessageCountToPublish;
            Console.WriteLine("Time per message: {0} ms", publishAverage);

            SpinWait.SpinUntil(() => resultList.Count == MessageCountToPublish);

            Assert.IsTrue(resultList.Count == MessageCountToPublish);
            Assert.IsTrue(resultList.TrueForAll(x => x == str), "Result does not contain expected result \"{0}\" repeated {1} times.", str, StrLength);
            Assert.IsTrue(publishAverage < 50, "Publish took too long.");
        }

        /// <summary>
        /// Publish 500 messages and ensure that the messages are received back.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        [Test]
        public async Task Publish500OneKBMessages()
        {
            const int MessageCountToPublish = 500;
            const int MessageSizeToPublish = 1024;
            var resultList = new List<byte[]>();
            await _testBus.Subscribe((byte[] s) =>
            {
                resultList.Add(s);
                return null;
            });

            var elapsed = await TimedPublishByte(MessageSizeToPublish, MessageCountToPublish);
            var publishAverage = elapsed.TotalMilliseconds / MessageCountToPublish;
            Console.WriteLine("Time per message: {0} ms", publishAverage);

            SpinWait.SpinUntil(() => resultList.Count == MessageCountToPublish);

            Assert.IsTrue(resultList.Count == MessageCountToPublish, "Result size is incorrect.");
            Assert.IsTrue(resultList.TrueForAll(x => x.Length == MessageSizeToPublish), "Response does not contain expected result.");
            Assert.IsTrue(publishAverage < 50, "Publish took too long.");
        }

        /// <summary>
        /// The tear down.
        /// </summary>
        [TestFixtureTearDown]
        public void TearDown()
        {
            _testBus.Dispose();
        }

        ~PublishTests()
        {
            _testBus.Dispose();
        }

        private async Task<TimeSpan> TimedPublishByte(int size, int count)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
                await _testBus.Send(new byte[size]);
            sw.Stop();
            return sw.Elapsed;
        }

        private async Task<TimeSpan> TimedPublishManyString(int count, string str)
        {
            var sw = Stopwatch.StartNew();
            var messagesToSend = new List<string>();
            for (int i = 0; i < count; i++)
            {
                messagesToSend.Add(str);
            }

            await _testBus.Send(messagesToSend);

            sw.Stop();
            return sw.Elapsed;
        }
    }
}
