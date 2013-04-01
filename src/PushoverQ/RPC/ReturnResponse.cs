using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ.RPC
{
    class ReturnResponse
    {
        public Exception Exception { get; set; }
        public object ReturnValue { get; set; }
    }
}
