using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ.Configuration
{
    public class BusConfigurator
    {
        internal BusSettings Settings { get; private set; }

        internal BusConfigurator()
        {
            Settings = new BusSettings();
        }

        public void WithConnectionString(string connectionString)
        {
            Settings.ConnectionString = connectionString.Replace("localhost", Environment.MachineName);
        }

        public void WithDefaultSubscriptionName(string subscription)
        {
            Settings.EndpointName = subscription;
        }

        public void WithSubscriptionLookupFunction(Func<Type, string> lookup)
        {
            Settings.TypeToSubscriptionName = lookup;
        }

        public void WithTopicLookupFunction(Func<Type, string> lookup)
        {
            Settings.TypeToTopicName = lookup;
        }

        public void WithSerializer<T>() where T : ISerializer, new()
        {
            Settings.Serializer = new T();
        }

        public void WithNumberOfReceiversPerSubscription(uint count)
        {
            Settings.NumberOfReceiversPerSubscription = count;
        }

        public void WithSerializer(ISerializer serializer)
        {
            Settings.Serializer = serializer;
        }
    }
}
