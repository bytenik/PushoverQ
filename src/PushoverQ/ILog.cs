using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PushoverQ
{
    /// <summary>
    /// A simple logging interface abstracting logging APIs.
    /// </summary>
    public interface ILog
    {
        /// <summary>
        /// Log a message with the Trace level.
        /// </summary>
        /// <param name="format">The format of the message object to log. <see cref="M:System.String.Format(System.String,System.Object[])"/></param>
        /// <param name="args">The list of format arguments</param>
        void Trace(string format, params object[] args);

        /// <summary>
        /// Log a message with the Trace level.
        /// </summary>
        /// <param name="exception">The exception to log.</param>
        /// <param name="format">The format of the message object to log. <see cref="M:System.String.Format(System.String,System.Object[])"/></param>
        /// <param name="args">the list of format arguments</param>
        void Trace(Exception exception, string format, params object[] args);
        
        /// <summary>
        /// Log a message with the Debug level.
        /// </summary>
        /// <param name="format">The format of the message object to log. <see cref="M:System.String.Format(System.String,System.Object[])"/></param>
        /// <param name="args">the list of format arguments</param>
        void Debug(string format, params object[] args);
        
        /// <summary>
        /// Log a message with the Debug level.
        /// </summary>
        /// <param name="exception">The exception to log.</param>
        /// <param name="format">The format of the message object to log. <see cref="M:System.String.Format(System.String,System.Object[])"/></param>
        /// <param name="args">the list of format arguments</param>
        void Debug(Exception exception, string format, params object[] args);
        
        /// <summary>
        /// Log a message with the Info level.
        /// </summary>
        /// <param name="format">The format of the message object to log. <see cref="M:System.String.Format(System.String,System.Object[])"/></param>
        /// <param name="args">the list of format arguments</param>
        void Info(string format, params object[] args);
        
        /// <summary>
        /// Log a message with the Info level.
        /// </summary>
        /// <param name="exception">The exception to log.</param>
        /// <param name="format">The format of the message object to log.<see cref="M:System.String.Format(System.String,System.Object[])"/></param>
        /// <param name="args">the list of format arguments</param>
        void Info(Exception exception, string format, params object[] args);

        /// <summary>
        /// Log a message with the Warn level.
        /// </summary>
        /// <param name="format">The format of the message object to log.<see cref="M:System.String.Format(System.String,System.Object[])"/></param>
        /// <param name="args">the list of format arguments</param>
        void Warn(string format, params object[] args);
        
        /// <summary>
        /// Log a message with the Warn level.
        /// </summary>
        /// <param name="exception">The exception to log.</param>
        /// <param name="format">The format of the message object to log.<see cref="M:System.String.Format(System.String,System.Object[])"/></param>
        /// <param name="args">the list of format arguments</param>
        void Warn(Exception exception, string format, params object[] args);

        /// <summary>
        /// Log a message with the Error level.
        /// </summary>
        /// <param name="format">The format of the message object to log.<see cref="M:System.String.Format(System.String,System.Object[])"/></param>
        /// <param name="args">the list of format arguments</param>
        void Error(string format, params object[] args);

        /// <summary>
        /// Log a message with the Error level.
        /// </summary>
        /// <param name="exception">The exception to log.</param>
        /// <param name="format">The format of the message object to log.<see cref="M:System.String.Format(System.String,System.Object[])"/></param>
        /// <param name="args">the list of format arguments</param>
        void Error(Exception exception, string format, params object[] args);
        
        /// <summary>
        /// Log a message with the Fatal level.
        /// </summary>
        /// <param name="format">The format of the message object to log.<see cref="M:System.String.Format(System.String,System.Object[])"/></param>
        /// <param name="args">the list of format arguments</param>
        void Fatal(string format, params object[] args);
        
        /// <summary>
        /// Log a message with the Fatal level.
        /// </summary>
        /// <param name="exception">The exception to log.</param>
        /// <param name="format">The format of the message object to log.<see cref="M:System.String.Format(System.String,System.Object[])"/></param>
        /// <param name="args">the list of format arguments</param>
        void Fatal(Exception exception, string format, params object[] args);
    }
}