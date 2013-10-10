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

        public void WithEndpointName(string endpointName)
        {
            Settings.EndpointName = endpointName;
        }

        public void WithApplicationName(string applicationName)
        {
            Settings.ApplicationName = applicationName;
        }

        public void WithSerializer<T>() where T : ISerializer, new()
        {
            Settings.Serializer = new T();
        }

        public void WithTopicResolver(Func<Type, string> resolver)
        {
            Settings.TopicNameResolver = resolver;
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
