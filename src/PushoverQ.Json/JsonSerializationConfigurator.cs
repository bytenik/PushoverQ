using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PushoverQ.Configuration;

namespace PushoverQ.Json
{
    public static class JsonSerializationConfigurator
    {
        public static void WithJsonSerialization(this BusConfigurator configurator)
        {
            configurator.WithSerializer(new BusJsonSerializer());
        }

        public static void WithJsonSerialization(this BusConfigurator configurator, JsonSerializerSettings settings)
        {
            configurator.WithSerializer(new BusJsonSerializer(settings));
        }
    }
}
