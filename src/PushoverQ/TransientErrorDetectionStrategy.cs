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
        /// <summary>
        /// Determines whether the specified exception represents a transient failure that can be compensated by a retry.
        /// </summary>
        /// <param name="ex">The exception object to be verified.</param>
        /// <returns>
        /// True if the specified exception is considered as transient, otherwise false.
        /// </returns>
        public bool IsTransient(Exception ex)
        {
            if (ex is ServerBusyException)
                return true;
            if (ex is TimeoutException && !(ex is TaskExtensions.NaiveTimeoutException))
                return true;
            if (ex is ServerTooBusyException)
                return true;
            if (ex is MessagingCommunicationException)
                return ((MessagingCommunicationException)ex).IsTransient
                    || ex.Message.Contains("The socket connection was aborted.");
            if (ex is MessagingException)
                return ((MessagingException)ex).IsTransient
                    || ex.Message.Contains("please retry the operation")
                    || ex.Message.Contains("service was not avaliable")
                    || ex.Message.Contains("service was not available")
                    || ex.Message.Contains("Provider Internal Error")
                    || ex.Message.Contains("Another conflicting operation is in progress");
            if (ex is CommunicationException)
                return true;
            if (ex is SocketException)
                return ((SocketException)ex).ErrorCode == (int)SocketError.TimedOut
                    || ((SocketException)ex).ErrorCode == (int)SocketError.ConnectionReset;
            if (ex is UnauthorizedAccessException)
                return ex.Message.Contains("The remote name could not be resolved") 
                    || ex.Message.Contains("The underlying connection was closed")
                    || ex.Message.Contains("Unable to connect to the remote server");
            if (ex is AggregateException)
                return ((AggregateException)ex).InnerExceptions.All(IsTransient);

            return false;
        }
    }
}
