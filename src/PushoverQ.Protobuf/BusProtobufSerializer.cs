using System;
using System.IO;
using System.Threading;
using ProtoBuf;


        //public void Serialize<TEntity>(TEntity entity, Stream stream)
        //{
        //    // ProtoBuf must have non-zero files
        //    stream.WriteByte(42);
        //    Serializer.Serialize(stream, entity);
        //}

        //public TEntity Deserialize<TEntity>(Stream stream)
        //{
        //    var signature = stream.ReadByte();

        //    if (signature != 42)
        //        throw new InvalidOperationException("Unknown view format");

        //    return Serializer.Deserialize<TEntity>(stream);
        //}

namespace PushoverQ.Protobuf
{
    class BusProtobufSerializer : ISerializer
    {

        public BusProtobufSerializer()
        {
        }

        public void Serialize(object obj, Stream stream)
        {
            // ProtoBuf must have non-zero files
            stream.WriteByte(42);

            Serializer.Serialize(stream, obj);
        }

        public T Deserialize<T>(Stream stream) where T : class
        {
            var signature = stream.ReadByte();

            if (signature != 42)
                throw new InvalidOperationException("Unknown stream for protobuf");

            return Serializer.Deserialize<T>(stream);
        }

        public object Deserialize(Type type, Stream stream)
        {
            var signature = stream.ReadByte();

            if (signature != 42)
                throw new InvalidOperationException("Unknown stream for protobuf");

            return Serializer.NonGeneric.Deserialize(type, stream);
        }
    }
}
