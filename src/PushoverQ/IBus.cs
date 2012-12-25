using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PushoverQ.ContextConfiguration;
using PushoverQ.SendConfiguration;

namespace PushoverQ
{
    public interface IBus : IDisposable
    {
        Task Publish(object message);
        Task Publish(object message, TimeSpan timeout);
        Task Publish(object message, CancellationToken token);
        Task Publish(object message, TimeSpan timeout, CancellationToken token);

        Task Publish(object message, Action<ISendConfigurator> configure);
        Task Publish(object message, Action<ISendConfigurator> configure, TimeSpan timeout);
        Task Publish(object message, Action<ISendConfigurator> configure, CancellationToken token);
        Task Publish(object message, Action<ISendConfigurator> configure, TimeSpan timeout, CancellationToken token);

        Task<T> Publish<T>(object message);
        Task<T> Publish<T>(object message, TimeSpan timeout);
        Task<T> Publish<T>(object message, CancellationToken token);
        Task<T> Publish<T>(object message, TimeSpan timeout, CancellationToken token);

        Task<T> Publish<T>(object message, Action<ISendConfigurator> configure);
        Task<T> Publish<T>(object message, Action<ISendConfigurator> configure, TimeSpan timeout);
        Task<T> Publish<T>(object message, Action<ISendConfigurator> configure, CancellationToken token);
        Task<T> Publish<T>(object message, Action<ISendConfigurator> configure, TimeSpan timeout, CancellationToken token);

        Task<ISubscription> Subscribe(string topic, string subscription);
        
        Task<ISubscription> Subscribe<T>(Func<T, Task> handler);
        Task<ISubscription> Subscribe<T>(string subscription, Func<T, Task> handler);
        Task<ISubscription> Subscribe<T>(Func<T, Envelope, Task> handler);
        Task<ISubscription> Subscribe<T>(string subscription, Func<T, Envelope, Task> handler);

        Task<ISubscription> Subscribe<T>(Consumes<T>.Message consumer);
        Task<ISubscription> Subscribe<T>(string subscription, Consumes<T>.Message consumer);
        Task<ISubscription> Subscribe<T>(Consumes<T>.Envelope consumer);
        Task<ISubscription> Subscribe<T>(string subscription, Consumes<T>.Envelope consumer);

        Task<ISubscription> Subscribe<T>(Func<Consumes<T>.Message> consumerFactory);
        Task<ISubscription> Subscribe<T>(string subscription, Func<Consumes<T>.Message> consumerFactory);
        Task<ISubscription> Subscribe<T>(Func<Consumes<T>.Envelope> consumerFactory);
        Task<ISubscription> Subscribe<T>(string subscription, Func<Consumes<T>.Envelope> consumerFactory);
    }
}
