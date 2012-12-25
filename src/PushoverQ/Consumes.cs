using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ
{
    public class Consumes<T>
    {
        public interface Message
        {
            Task Consume(T message);
        }

        public interface Envelope
        {
            Task Consume(T message, PushoverQ.Envelope envelope);
        }
    }
}
