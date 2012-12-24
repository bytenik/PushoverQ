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
        public string CompeteSubscriptionName { get; set; }

        public BusSettings()
        {
            TypeToTopicName = type => type.FullName;
            CompeteSubscriptionName = "default";
        }
    }
}
