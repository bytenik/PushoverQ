using System;

namespace PushoverQ.RPC
{
    class MethodCallCommand
    {
        public string MethodName { get; set; }
        public string[] ArgumentTypes { get; set; }
        public object[] Arguments { get; set; }
    }
}
