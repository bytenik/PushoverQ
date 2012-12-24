using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ.Configuration
{
    public class BusConfigurator
    {
        internal BusSettings Settings { get; private set; }

        internal BusConfigurator()
        {
            Settings = new BusSettings();
        }
    }
}
