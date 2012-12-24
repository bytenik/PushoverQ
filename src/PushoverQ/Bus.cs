using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PushoverQ.Configuration;
using PushoverQ.ContextConfiguration;

namespace PushoverQ
{
    public sealed class Bus : IBus, IDisposable
    {
        private readonly BusSettings _settings;

        public static IBus CreateBus(Action<BusConfigurator> configure)
        {
            var configurator = new BusConfigurator();
            configure(configurator);

            return new Bus(configurator.Settings);
        }

        private Bus(BusSettings settings)
        {
            _settings = settings;
        }

        #region Publish

        #region Publish wrapper overloads
        public Task Publish(object message)
        {
            return Publish(message, Timeout.InfiniteTimeSpan, CancellationToken.None);
        }

        public Task Publish(object message, TimeSpan timeout)
        {
            return Publish(message, timeout, CancellationToken.None);
        }

        public Task Publish(object message, CancellationToken token)
        {
            return Publish(message, Timeout.InfiniteTimeSpan, token);
        }
        #endregion

        public Task Publish(object message, TimeSpan timeout, CancellationToken token)
        {
            return Publish(message, null, timeout, token);
        }

        #region Publish w/ configure wrapper overloads
        public Task Publish(object message, Action<IContextConfigurator> configure)
        {
            return Publish(message, configure, Timeout.InfiniteTimeSpan, CancellationToken.None);
        }

        public Task Publish(object message, Action<IContextConfigurator> configure, TimeSpan timeout)
        {
            return Publish(message, configure, timeout, CancellationToken.None);
        }

        public Task Publish(object message, Action<IContextConfigurator> configure, CancellationToken token)
        {
            return Publish(message, configure, Timeout.InfiniteTimeSpan, token);
        }
        #endregion

        public Task Publish(object message, Action<IContextConfigurator> configure, TimeSpan timeout, CancellationToken token)
        {
            var configurator = new ContextConfigurator();
            if(configure != null) configure(configurator);

            throw new NotImplementedException();
        }

        #region Publish<T> wrapper overloads
        public Task<T> Publish<T>(object message)
        {
            return Publish<T>(message, Timeout.InfiniteTimeSpan, CancellationToken.None);            
        }

        public Task<T> Publish<T>(object message, TimeSpan timeout)
        {
            return Publish<T>(message, timeout, CancellationToken.None);
        }

        public Task<T> Publish<T>(object message, CancellationToken token)
        {
            return Publish<T>(message, Timeout.InfiniteTimeSpan, token);
        }
        #endregion

        public Task<T> Publish<T>(object message, TimeSpan timeout, CancellationToken token)
        {
            return Publish<T>(message, null, timeout, token);
        }

        public Task<T> Publish<T>(object message, Action<IContextConfigurator> configure)
        {
            return Publish<T>(message, configure, Timeout.InfiniteTimeSpan, CancellationToken.None);
        }

        public Task<T> Publish<T>(object message, Action<IContextConfigurator> configure, TimeSpan timeout)
        {
            return Publish<T>(message, configure, timeout, CancellationToken.None);
        }

        public Task<T> Publish<T>(object message, Action<IContextConfigurator> configure, CancellationToken token)
        {
            return Publish<T>(message, configure, Timeout.InfiniteTimeSpan, token);
        }

        public Task<T> Publish<T>(object message, Action<IContextConfigurator> configure, TimeSpan timeout, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Subscribe

        public Task Subscribe<T>(Func<T, Task> handler)
        {
            return Subscribe(_settings.CompeteSubscriptionName, handler);
        }

        public Task Subscribe<T>(string subscription, Func<T, Task> handler)
        {
            return Subscribe(_settings.TypeToTopicName(typeof(T)), subscription, handler);
        }

        public Task Subscribe<T>(string topic, string subscription, Func<T, Task> handler)
        {
            throw new NotImplementedException();
        }

        public Task Subscribe<T>(Func<T, IContext, Task> handler)
        {
            return Subscribe(_settings.CompeteSubscriptionName, handler);
        }

        public Task Subscribe<T>(string subscription, Func<T, IContext, Task> handler)
        {
            return Subscribe(_settings.TypeToTopicName(typeof(T)), subscription, handler);
        }

        public Task Subscribe<T>(string topic, string subscription, Func<T, IContext, Task> handler)
        {
            throw new NotImplementedException();
        }

        public Task Subscribe<T>(Consumes<T>.All consumer)
        {
            return Subscribe(_settings.CompeteSubscriptionName, consumer);
        }

        public Task Subscribe<T>(string subscription, Consumes<T>.All consumer)
        {
            return Subscribe(_settings.TypeToTopicName(typeof(T)), subscription, consumer);
        }

        public Task Subscribe<T>(string topic, string subscription, Consumes<T>.All consumer)
        {
            throw new NotImplementedException();
        }

        public Task Subscribe<T>(Consumes<T>.Context consumer)
        {
            return Subscribe(_settings.CompeteSubscriptionName, consumer);
        }

        public Task Subscribe<T>(string subscription, Consumes<T>.Context consumer)
        {
            return Subscribe(_settings.TypeToTopicName(typeof(T)), subscription, consumer);
        }

        public Task Subscribe<T>(string topic, string subscription, Consumes<T>.Context consumer)
        {
            throw new NotImplementedException();
        }

        public Task Subscribe<T>(Func<Consumes<T>.All> consumerFactory)
        {
            return Subscribe(_settings.CompeteSubscriptionName, consumerFactory);
        }

        public Task Subscribe<T>(string subscription, Func<Consumes<T>.All> consumerFactory)
        {
            return Subscribe(_settings.TypeToTopicName(typeof(T)), subscription, consumerFactory);
        }

        public Task Subscribe<T>(string topic, string subscription, Func<Consumes<T>.All> consumerFactory)
        {
            throw new NotImplementedException();
        }

        public Task Subscribe<T>(Func<Consumes<T>.Context> consumerFactory)
        {
            return Subscribe(_settings.CompeteSubscriptionName, consumerFactory);
        }

        public Task Subscribe<T>(string subscription, Func<Consumes<T>.Context> consumerFactory)
        {
            return Subscribe(_settings.TypeToTopicName(typeof (T)), subscription, consumerFactory);
        }

        public Task Subscribe<T>(string topic, string subscription, Func<Consumes<T>.Context> consumerFactory)
        {
            throw new NotImplementedException();
        }

        #endregion

        public void Dispose(bool disposing)
        {
            
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~Bus()
        {
            Dispose(false);            
        }
    }
}
