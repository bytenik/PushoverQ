using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PushoverQ.SendConfiguration;

namespace PushoverQ
{
    public static class BusExtensions
    {
        public static Task Publish(this IBus bus, object message)
        {
            return bus.Publish(message, Timeout.InfiniteTimeSpan, CancellationToken.None);
        }

        public static Task Publish(this IBus bus, object message, TimeSpan timeout)
        {
            return bus.Publish(message, timeout, CancellationToken.None);
        }

        public static Task Publish(this IBus bus, object message, CancellationToken token)
        {
            return bus.Publish(message, Timeout.InfiniteTimeSpan, token);
        }

        public static Task Publish(this IBus bus, object message, Action<ISendConfigurator> configure)
        {
            return bus.Publish(message, configure, Timeout.InfiniteTimeSpan, CancellationToken.None);
        }

        public static Task Publish(this IBus bus, object message, Action<ISendConfigurator> configure, TimeSpan timeout)
        {
            return bus.Publish(message, configure, timeout, CancellationToken.None);
        }

        public static Task Publish(this IBus bus, object message, Action<ISendConfigurator> configure, CancellationToken token)
        {
            return bus.Publish(message, configure, Timeout.InfiniteTimeSpan, token);
        }

        public static Task<T> Publish<T>(this IBus bus, object message)
        {
            return bus.Publish<T>(message, Timeout.InfiniteTimeSpan, CancellationToken.None);
        }

        public static Task<T> Publish<T>(this IBus bus, object message, TimeSpan timeout)
        {
            return bus.Publish<T>(message, timeout, CancellationToken.None);
        }

        public static Task<T> Publish<T>(this IBus bus, object message, CancellationToken token)
        {
            return bus.Publish<T>(message, Timeout.InfiniteTimeSpan, token);
        }

        public static Task<T> Publish<T>(this IBus bus, object message, Action<ISendConfigurator> configure)
        {
            return bus.Publish<T>(message, configure, Timeout.InfiniteTimeSpan, CancellationToken.None);
        }

        public static Task<T> Publish<T>(this IBus bus, object message, Action<ISendConfigurator> configure, TimeSpan timeout)
        {
            return bus.Publish<T>(message, configure, timeout, CancellationToken.None);
        }

        public static Task<T> Publish<T>(this IBus bus, object message, Action<ISendConfigurator> configure, CancellationToken token)
        {
            return bus.Publish<T>(message, configure, Timeout.InfiniteTimeSpan, token);
        }
    }
}
