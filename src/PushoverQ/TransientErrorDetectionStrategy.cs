using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.ServiceModel;
using System.Text;
using Microsoft.Practices.TransientFaultHandling;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace MassTransit.Transports.MicrosoftServiceBus
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
            if (ex is TimeoutException)
                return true;
            if (ex is ServerTooBusyException)
                return true;
            if (ex is MessagingCommunicationException)
                return true;
            if (ex is CommunicationException)
                return true;
            if (ex is SocketException)
                return ((SocketException)ex).ErrorCode == (int) SocketError.TimedOut;
            if (ex is UnauthorizedAccessException)
                return ex.Message.Contains("The remote name could not be resolved");

            return false;
        }
    }
}
