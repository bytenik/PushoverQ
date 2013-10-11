using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ
{
    /// <summary>
    /// The built-in consumer class.
    /// </summary>
    /// <typeparam name="T"> The type of consumed message. </typeparam>
    public class Consumes<T> where T : class
    {
        /// <summary>
        /// The interface for consumers that take only the message.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1302:InterfaceNamesMustBeginWithI", Justification = "Sub-interfaces are awkward when starting with I.")]
        public interface Message : IConsumer
        {
            /// <summary>
            /// Consumes the message.
            /// </summary>
            /// <param name="message"> The message. </param>
            /// <returns> The <see cref="Task"/> that completes when processing has concluded, or null for non-forking consumers. </returns>
            Task Consume(T message);
        }

        /// <summary>
        /// The interface for consumers that take the message and the envelope information.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1302:InterfaceNamesMustBeginWithI", Justification = "Sub-interfaces are awkward when starting with I.")]
        public interface Envelope : IConsumer
        {
            /// <summary>
            /// Consumes the message.
            /// </summary>
            /// <param name="message"> The message. </param>
            /// <param name="envelope"> The envelope. </param>
            /// <returns> The <see cref="Task"/> that completes when processing has concluded, or null for non-forking consumers. </returns>
            Task Consume(T message, PushoverQ.Envelope envelope);
        }
    }
}
