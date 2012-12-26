using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ
{
    public class Consumes<T> where T : class
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
