using PushoverQ.Configuration;

namespace PushoverQ.Protobuf
{
    public static class ProtobufSerializationConfigurator
    {
        public static void WithProtobufSerialization(this BusConfigurator configurator)
        {
            configurator.WithSerializer(new BusProtobufSerializer());
        }
    }
}
