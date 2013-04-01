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
        Task Publish(object message, TimeSpan timeout, CancellationToken token);
        Task Publish(object message, Action<ISendConfigurator> configure, TimeSpan timeout, CancellationToken token);

        Task<T> Publish<T>(object message, TimeSpan timeout, CancellationToken token);
        Task<T> Publish<T>(object message, Action<ISendConfigurator> configure, TimeSpan timeout, CancellationToken token);

        void Attach<T>(Func<T, Task> handler) where T : class;
        void Attach<T>(Func<T, Envelope, Task> handler) where T : class;
        void Attach(Type type, Func<object, Envelope, Task> handler);

        Task<ISubscription> Subscribe(string topic, string subscription);
        Task<ISubscription> Subscribe(Type type, Func<object, Envelope, Task> handler);
        Task<ISubscription> Subscribe(string subscription, Type type, Func<object, Envelope, Task> handler);

        Task<ISubscription> Subscribe<T>() where T : class;
        Task<ISubscription> Subscribe<T>(string subscription) where T : class;
        Task<ISubscription> Subscribe(Type type);
        Task<ISubscription> Subscribe(string subscription, Type type);

        Task<ISubscription> Subscribe<T>(Func<T, Task> handler) where T : class;
        Task<ISubscription> Subscribe<T>(string subscription, Func<T, Task> handler) where T : class;
        Task<ISubscription> Subscribe<T>(Func<T, Envelope, Task> handler) where T : class;
        Task<ISubscription> Subscribe<T>(string subscription, Func<T, Envelope, Task> handler) where T : class;

        Task<ISubscription> Subscribe<T>(Consumes<T>.Message consumer) where T : class;
        Task<ISubscription> Subscribe<T>(string subscription, Consumes<T>.Message consumer) where T : class;
        Task<ISubscription> Subscribe<T>(Consumes<T>.Envelope consumer) where T : class;
        Task<ISubscription> Subscribe<T>(string subscription, Consumes<T>.Envelope consumer) where T : class;

        Task<ISubscription> Subscribe<T>(Func<Consumes<T>.Message> consumerFactory) where T : class;
        Task<ISubscription> Subscribe<T>(string subscription, Func<Consumes<T>.Message> consumerFactory) where T : class;
        Task<ISubscription> Subscribe<T>(Func<Consumes<T>.Envelope> consumerFactory) where T : class;
        Task<ISubscription> Subscribe<T>(string subscription, Func<Consumes<T>.Envelope> consumerFactory) where T : class;
    }
}
