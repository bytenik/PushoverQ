using System;

namespace PushoverQ.RPC
{
    class CallCommand
    {
        public string MethodName { get; set; }
        public Type[] TypeArguments { get; set; }
        public object[] Arguments { get; set; }
    }
}
