using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ.Configuration
{
    class BusSettings
    {
        /// <summary>
        /// Gets or sets the name of this specific endpoint. This name must be unique on the service bus for pub/sub to work properly.
        /// </summary>
        public string EndpointName { get; set; }

        /// <summary>
        /// Gets or sets the name of the application. This name must be the same for all service bus users for competing consumers to work properly.
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// Gets or sets the serializer to use.
        /// </summary>
        public ISerializer Serializer { get; set; }

        /// <summary>
        /// Gets or sets the topic name resolver.
        /// </summary>
        public Func<Type, string> TopicNameResolver { get; set; } 

        /// <summary>
        /// Gets or sets the bus connection string.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of messages still "in-flight" (i.e. sending).
        /// </summary>
        public ushort MaxMessagesInFlight { get; set; }

        /// <summary>
        /// Gets or sets the number of receivers to create for each bus subscription. This is different than the number of topics to create per type.
        /// </summary>
        public uint NumberOfReceiversPerSubscription { get; set; }

        public BusSettings()
        {
            EndpointName = Environment.MachineName;
            ApplicationName = "app";
            MaxMessagesInFlight = 10;
            NumberOfReceiversPerSubscription = 5;
            TopicNameResolver = t => t.FullName.Replace("[]", "_Array");
            Serializer = new BinaryFormatterSerializer();
        }
    }
}
