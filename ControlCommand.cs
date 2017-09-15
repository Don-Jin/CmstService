using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SuperSocket.SocketBase;

namespace CmstService
{
    class ControlCommand
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public Func<IBootstrap, string[], bool> Handler { get; set; }
    }
}
