using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PushoverQ
{
    class BinaryFormatterSerializer : ISerializer
    {
        private static readonly ThreadLocal<BinaryFormatter> Formatter = new ThreadLocal<BinaryFormatter>(() => new BinaryFormatter()); 

        public void Serialize(object obj, Stream stream)
        {
            Formatter.Value.Serialize(stream, obj);
        }

        public T Deserialize<T>(Stream stream) where T : class
        {
            return Formatter.Value.Deserialize(stream) as T;
        }

        public object Deserialize(Type type, Stream stream)
        {
            return Formatter.Value.Deserialize(stream);
        }
    }
}
