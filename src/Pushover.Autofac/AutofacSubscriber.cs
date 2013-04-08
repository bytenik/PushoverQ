using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Autofac.Core;
using PushoverQ;
using PushoverQ.Configuration;

namespace Pushover.Autofac
{
    public static class AutofacSubscriber
    {
        static IEnumerable<Type> FindTypes<T>(IComponentContext scope)
        {
            return scope.ComponentRegistry.Registrations
                .SelectMany(r => r.Services.OfType<IServiceWithType>(), (r, s) => new { r, s })
                .Where(rs => typeof(T).IsAssignableFrom(rs.s.ServiceType))
                .Select(rs => rs.r.Activator.LimitType)
                .ToList();
        }

        public static async Task SubscribeFrom(this IBus bus, ILifetimeScope scope)
        {
            var consumerTypes = FindTypes<IConsumer>(scope);
            var subscribeTasks = consumerTypes
                .AsParallel()
                .SelectMany(handlerType =>
                {
                    return handlerType.GetInterfaces().Where(interfaceType => typeof(IConsumer).IsAssignableFrom(interfaceType))
                        .Select(interfaceType =>
                        {
                            var messageType = interfaceType.GetGenericArguments()[0];
                            return bus.Subscribe(messageType, (m, e) =>
                            {
                                var innerScope = scope.BeginLifetimeScope();
                                var consumer = innerScope.Resolve(handlerType);

                                Task task;
                                if (interfaceType.GetGenericTypeDefinition() == typeof(Consumes<>.Message))
                                {
                                    Expression<Action<Consumes<object>.Message>> sample = x => x.Consume(null);
                                    var method = (sample.Body as MethodCallExpression).Method;
                                    method = interfaceType.GetMethod(method.Name, new[] { messageType });
                                    task = method.Invoke(consumer, new[] { m }) as Task;
                                }
                                else if (interfaceType.GetGenericTypeDefinition() == typeof(Consumes<>.Envelope))
                                {
                                    Expression<Action<Consumes<object>.Envelope>> sample = x => x.Consume(null, null);
                                    var method = (sample.Body as MethodCallExpression).Method;
                                    method = interfaceType.GetMethod(method.Name, new[] { messageType, typeof(Envelope) });
                                    task = method.Invoke(consumer, new[] { m, e }) as Task;
                                }
                                else
                                    throw new NotSupportedException("Unsupported IConsumer derivitive.");

                                return task.ContinueWith(t => innerScope.Dispose());
                            });
                        });
                });

            await Task.WhenAll(subscribeTasks);
        }
    }
}
