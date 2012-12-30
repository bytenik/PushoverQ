using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PushoverQ.Json
{
    class BusJsonSerializer : ISerializer
    {
        private readonly JsonSerializerSettings _settings;

        public BusJsonSerializer(JsonSerializerSettings settings)
        {
            _settings = settings;
        }

        public BusJsonSerializer()
            : this(new JsonSerializerSettings())
        {
            _serializer = new ThreadLocal<JsonSerializer>(() => JsonSerializer.Create(_settings));
        }

        private readonly ThreadLocal<JsonSerializer> _serializer;

        public void Serialize(object obj, Stream stream)
        {
            using (var ms = new MemoryStream())
            using (var sw = new StreamWriter(ms) { AutoFlush = true })
            {
                _serializer.Value.Serialize(sw, obj);
                ms.Seek(0, SeekOrigin.Begin);
                ms.CopyTo(stream);
            }
        }

        public T Deserialize<T>(Stream stream) where T : class
        {
            using (var sr = new StreamReader(stream))
                return _serializer.Value.Deserialize<T>(new JsonTextReader(sr));
        }

        public object Deserialize(Type type, Stream stream)
        {
            using (var sr = new StreamReader(stream))
                return _serializer.Value.Deserialize(new JsonTextReader(sr), type);
        }
    }
}
