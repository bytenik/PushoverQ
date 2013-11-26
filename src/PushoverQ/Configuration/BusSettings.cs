using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ.Configuration
{
    /// <summary>
    /// The bus settings.
    /// </summary>
    class BusSettings
    {
        float _renewalThreshold;
        TimeSpan _lockDuration;

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

        /// <summary>
        /// Gets or sets the bus logger.
        /// </summary>
        public ILog Logger { get; set; }

        /// <summary>
        /// Gets or sets the percentage (expressed as a number between 0.0 and 1.0) of total lock time that should elapse before auto-renewing a lock.
        /// </summary>
        public float RenewalThreshold
        {
            get
            {
                return _renewalThreshold;
            }

            set
            {
                if (value < 0 || value > 1f)
                    throw new ArgumentOutOfRangeException("value", "Must be a value between 0 and 1");

                _renewalThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of times a message may be delivered from the bus before it is dead-lettered.
        /// </summary>
        public uint MaxDeliveryCount { get; set; }

        /// <summary>
        /// Gets or sets the duration for which a message should be locked. PushoverQ will attempt to renew the lock automatically based on
        /// the <see cref="RenewalThreshold"/>. However, if the machine entirely crashes, this is the worst case amount of time until another machine
        /// may work on a message.
        /// </summary>
        public TimeSpan LockDuration
        {
            get
            {
                return _lockDuration;
            }

            set
            {
                if (value <= TimeSpan.Zero || value > TimeSpan.FromMinutes(5))
                    throw new ArgumentOutOfRangeException("value", "Must be a value between 0 and 5 minutes.");
                _lockDuration = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to throw a <see cref="MessageSizeException"/> when a message is too large to send to the bus.
        /// If this option is set to false, the error will simply be logged and move on.
        /// </summary>
        public bool ThrowOnOversizeMessage { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusSettings"/> class.
        /// </summary>
        public BusSettings()
        {
            EndpointName = Environment.MachineName;
            ApplicationName = "app";
            MaxMessagesInFlight = 10;
            NumberOfReceiversPerSubscription = 5;
            TopicNameResolver = t => t.FullName.Replace("[]", "_Array").Right(50);
            Serializer = new BinaryFormatterSerializer();
            Logger = new BitBucketLogger();
            RenewalThreshold = 0.5f;
            MaxDeliveryCount = 5;
            LockDuration = TimeSpan.FromSeconds(60);
            ThrowOnOversizeMessage = true;
        }
    }
}
