using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ
{
    public class Envelope
    {
        public Guid MessageId { get; set; }
        public Guid InReplyTo { get; set; }
    }
}
