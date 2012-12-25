using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ
{
    public interface ISerializer
    {
        void Serialize(object obj, Stream stream);
        T Deserialize<T>(Stream stream);
        object Deserialize(Type type, Stream stream);
    }
}
