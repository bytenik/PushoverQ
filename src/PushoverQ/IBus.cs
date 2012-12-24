using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PushoverQ.ContextConfiguration;

namespace PushoverQ
{
    public interface IBus : IDisposable
    {
        Task Publish(object message);
        Task Publish(object message, TimeSpan timeout);
        Task Publish(object message, CancellationToken token);
        Task Publish(object message, TimeSpan timeout, CancellationToken token);

        Task Publish(object message, Action<IContextConfigurator> configure);
        Task Publish(object message, Action<IContextConfigurator> configure, TimeSpan timeout);
        Task Publish(object message, Action<IContextConfigurator> configure, CancellationToken token);
        Task Publish(object message, Action<IContextConfigurator> configure, TimeSpan timeout, CancellationToken token);

        Task<T> Publish<T>(object message);
        Task<T> Publish<T>(object message, TimeSpan timeout);
        Task<T> Publish<T>(object message, CancellationToken token);
        Task<T> Publish<T>(object message, TimeSpan timeout, CancellationToken token);

        Task Subscribe<T>(Func<T, Task> handler);
        Task Subscribe<T>(string subscription, Func<T, Task> handler);
        Task Subscribe<T>(string topic, string subscription, Func<T, Task> handler);
        Task Subscribe<T>(Func<T, IContext, Task> handler);
        Task Subscribe<T>(string subscription, Func<T, IContext, Task> handler);
        Task Subscribe<T>(string topic, string subscription, Func<T, IContext, Task> handler);

        Task Subscribe<T>(Consumes<T>.All consumer);
        Task Subscribe<T>(string subscription, Consumes<T>.All consumer);
        Task Subscribe<T>(string topic, string subscription, Consumes<T>.All consumer);
        Task Subscribe<T>(Consumes<T>.Context consumer);
        Task Subscribe<T>(string subscription, Consumes<T>.Context consumer);
        Task Subscribe<T>(string topic, string subscription, Consumes<T>.Context consumer);
    }
}
