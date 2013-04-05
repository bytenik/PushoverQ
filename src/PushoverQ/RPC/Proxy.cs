using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ.RPC
{
    /// <summary> Message-based proxy creator. </summary>
    /// <typeparam name="T"> The type of proxy instance this class will create. </typeparam>
    class Proxy<T> : RealProxy
    {
        private readonly IBus _bus;

        /// <summary>
        /// Initializes a new instance of the <see cref="Proxy{T}"/> class.
        /// </summary>
        /// <param name="bus"> The bus. </param>
        public Proxy(IBus bus)
            : base(typeof(T))
        {
            _bus = bus;
        }

        /// <summary>
        /// Handles incoming message from the caller.
        /// </summary>
        /// <param name="msg">The message to handle.</param>
        /// <returns>A return message.</returns>
        public override IMessage Invoke(IMessage msg)
        {
            var message = (IMethodCallMessage)msg;
            var wrapper = new MethodCallMessageWrapper(message);
            if (wrapper.ArgCount != wrapper.InArgCount)
                throw new NotSupportedException("Cannot proxy method calls with out-parameters via PushoverQ.");

            var method = (MethodInfo)wrapper.MethodBase;
            Debug.Assert(method != null, "method != null");

            var command = new MethodCallCommand
                {
                    MethodName = wrapper.MethodName,
                    ArgumentTypes = wrapper.MethodBase.GetParameters().Select(x => x.ParameterType.FullName).ToArray(),
                    Arguments = wrapper.InArgs
                };

            if (method.ReturnType == typeof(void))
            {
                _bus.Publish(command).Wait();
                return new ReturnMessage(null, new object[0], 0, message.LogicalCallContext, message);
            }
            else if (method.ReturnType == typeof(Task))
            {
                var task = _bus.Send(command, confirmation: true);
                return new ReturnMessage(task, new object[0], 0, message.LogicalCallContext, message);                
            }
            else if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var task = _bus.Send<object>(command);
                return new ReturnMessage(task, new object[0], 0, message.LogicalCallContext, message);
            }
            else
                throw new NotSupportedException("Methods must return a Task or void.");
        }

        /// <summary>
        /// Gets the proxy instance.
        /// </summary>
        /// <returns>The instance.</returns>
        public new T GetTransparentProxy()
        {
            return (T)base.GetTransparentProxy();
        }
    }

}
