using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ.Configuration
{
    class BusSettings
    {
        public Func<Type, string> TypeToTopicName { get; set; }
        public Func<Type, string> TypeToSubscriptionName { get; set; }
        public string DefaultSubscriptionName { get; set; }
        public ISerializer Serializer { get; set; }
        public string ConnectionString { get; set; }
        public ushort MaxMessagesInFlight { get; set; }
        public uint NumberOfReceiversPerSubscription { get; set; }

        public BusSettings()
        {
            TypeToTopicName = type => type.FullName;
            TypeToSubscriptionName = type => DefaultSubscriptionName;
            DefaultSubscriptionName = "default";
            MaxMessagesInFlight = 10;
            NumberOfReceiversPerSubscription = 5;
            Serializer = new BinaryFormatterSerializer();
        }
    }
}
