using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ
{
    /// <summary>
    /// The message size exception.
    /// </summary>
    public class MessageSizeException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageSizeException"/> class.
        /// </summary>
        public MessageSizeException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageSizeException"/> class.
        /// </summary>
        /// <param name="message"> The error message. </param>
        public MessageSizeException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageSizeException"/> class.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <param name="innerException">
        /// The inner exception.
        /// </param>
        public MessageSizeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
