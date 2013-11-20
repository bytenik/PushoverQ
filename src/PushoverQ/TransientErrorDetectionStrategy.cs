using System;
using System.Linq;
using System.Net.Sockets;
using System.ServiceModel;
using Microsoft.Practices.TransientFaultHandling;
using Microsoft.ServiceBus.Messaging;

namespace PushoverQ
{
    class TransientErrorDetectionStrategy : ITransientErrorDetectionStrategy
    {
        public ILog Logger { get; set; }

        /// <summary>
        /// Determines whether the specified exception represents a transient failure that can be compensated by a retry.
        /// </summary>
        /// <param name="ex">The exception object to be verified.</param>
        /// <returns>
        /// True if the specified exception is considered as transient, otherwise false.
        /// </returns>
        public bool IsTransient(Exception ex)
        {
            var errorType = GetErrorType(ex);

            switch (errorType)
            {
                case ErrorType.SilentlyRetry:
                    return true;
                case ErrorType.RetryAndLog:
                    Logger.Warn(ex, "Service bus threw an exception; operation will be retried");
                    return true;
                case ErrorType.Fail:
                    return false;
                default:
                    throw new NotSupportedException("Unknown error type");
            }
        }

        private enum ErrorType
        {
            SilentlyRetry,
            RetryAndLog,
            Fail
        }

        private static ErrorType GetErrorType(Exception ex)
        {
            if (ex is ServerBusyException)
                return ErrorType.RetryAndLog;
            if (ex is TimeoutException && !(ex is TaskExtensions.NaiveTimeoutException))
                return ErrorType.RetryAndLog;
            if (ex is ServerTooBusyException)
                return ErrorType.RetryAndLog;
            
            var mce = ex as MessagingCommunicationException;
            if (mce != null)
            {
                if (mce.IsTransient)
                    return ErrorType.SilentlyRetry;
                else if (mce.ToString().Contains("The socket connection was aborted."))
                    return ErrorType.RetryAndLog;
            }

            var me = ex as MessagingException;
            if (me != null)
            {
                if (me.IsTransient
                    || me.Message.Contains("Another conflicting operation is in progress"))
                {
                    return ErrorType.SilentlyRetry;
                }
                else if (me.Message.Contains("please retry the operation")
                    || me.Message.Contains("service was not avaliable")
                    || me.Message.Contains("service was not available")
                    || me.Message.Contains("Provider Internal Error"))
                {
                    return ErrorType.RetryAndLog;
                }
            }

            if (ex is CommunicationException)
                return ErrorType.RetryAndLog;
            
            if (ex is SocketException)
                return ErrorType.RetryAndLog;

            if (ex is UnauthorizedAccessException
                && (ex.Message.Contains("The remote name could not be resolved")
                    || ex.Message.Contains("The underlying connection was closed")
                    || ex.Message.Contains("Unable to connect to the remote server")))
            {
                return ErrorType.RetryAndLog;
            }

            if (ex is AggregateException)
                return ((AggregateException)ex).InnerExceptions.Select(GetErrorType).Max();

            return ErrorType.Fail;
        }
    }
}
