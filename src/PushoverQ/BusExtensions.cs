using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PushoverQ
{
    /// <summary>
    /// The bus extensions.
    /// </summary>
    public static class BusExtensions
    {
        public static Task<ISubscription> Subscribe<T>(this IBus bus, Consumes<T>.Message consumer) where T : class
        {
            return bus.Subscribe<T>(() => consumer);
        }

        public static async Task<T> Consume<T>(this IBus bus) where T : class
        {
            var tcs = new TaskCompletionSource<T>();
            using (var task = bus.Subscribe<T>((m, e) =>
            {
                tcs.TrySetResult(m);
                return null;
            }))
            {
                task.Wait();
                return await tcs.Task;
            }
        }

        public static Task<ISubscription> Subscribe<T>(this IBus bus, string topic, string subscription, Consumes<T>.Message consumer) where T : class
        {
            return bus.Subscribe<T>(topic, subscription, () => consumer);
        }

        public static Task<ISubscription> Subscribe<T>(this IBus bus, Consumes<T>.Envelope consumer) where T : class
        {
            return bus.Subscribe<T>(() => consumer);
        }

        public static Task<ISubscription> Subscribe<T>(this IBus bus, string topic, string subscription, Consumes<T>.Envelope consumer) where T : class
        {
            return bus.Subscribe<T>(topic, subscription, () => consumer);            
        }

        public static Task<ISubscription> Subscribe<T>(this IBus bus, Func<Consumes<T>.Message> consumerFactory) where T : class
        {
            return bus.Subscribe<T>(m => consumerFactory().Consume(m));
        }

        public static Task<ISubscription> Subscribe<T>(this IBus bus, string topic, string subscription, Func<Consumes<T>.Message> consumerFactory) where T : class
        {
            return bus.Subscribe(topic, subscription, (m, e) => m is T ? consumerFactory().Consume((T)m) : null);            
        }

        public static Task<ISubscription> Subscribe<T>(this IBus bus, Func<T, Task> handler) where T : class
        {
            return bus.Subscribe<T>((m, e) => handler(m));
        }

        public static Task<ISubscription> Subscribe<T>(this IBus bus, Func<Consumes<T>.Envelope> consumerFactory) where T : class
        {
            return bus.Subscribe<T>((m, e) => consumerFactory().Consume(m, e));
        }

        public static Task<ISubscription> Subscribe<T>(this IBus bus, string topic, string subscription, Func<Consumes<T>.Envelope> consumerFactory) where T : class
        {
            return bus.Subscribe(topic, subscription, (m, e) => m is T ? consumerFactory().Consume((T)m, e) : null);
        }
    }
}
