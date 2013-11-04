using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ.Configuration
{
    /// <summary>
    /// The bus configurator.
    /// </summary>
    public class BusConfigurator
    {
        /// <summary>
        /// Gets the settings.
        /// </summary>
        internal BusSettings Settings { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusConfigurator"/> class.
        /// </summary>
        internal BusConfigurator()
        {
            Settings = new BusSettings();
        }

        public BusConfigurator WithConnectionString(string connectionString)
        {
            Settings.ConnectionString = connectionString.Replace("localhost", Environment.MachineName);
            return this;
        }

        public BusConfigurator WithEndpointName(string endpointName)
        {
            Settings.EndpointName = endpointName;
            return this;
        }

        public BusConfigurator WithApplicationName(string applicationName)
        {
            Settings.ApplicationName = applicationName;
            return this;
        }

        public BusConfigurator WithSerializer<T>() where T : ISerializer, new()
        {
            Settings.Serializer = new T();
            return this;
        }

        public BusConfigurator WithTopicResolver(Func<Type, string> resolver)
        {
            Settings.TopicNameResolver = resolver;
            return this;
        }

        public BusConfigurator WithNumberOfReceiversPerSubscription(uint count)
        {
            Settings.NumberOfReceiversPerSubscription = count;
            return this;
        }

        public BusConfigurator WithSerializer(ISerializer serializer)
        {
            Settings.Serializer = serializer;
            return this;
        }

        public BusConfigurator WithLogger(ILog logger)
        {
            Settings.Logger = logger;
            return this;
        }

        public BusConfigurator WithRenewalThreshold(float threshold)
        {
            Settings.RenewalThreshold = threshold;
            return this;
        }
        public BusConfigurator WithMaxDeliveryCount(uint max)
        {
            Settings.MaxDeliveryCount = max;
            return this;
        }

        public BusConfigurator WithLockDuration(TimeSpan duration)
        {
            Settings.LockDuration = duration;
            return this;
        }
    }
}
