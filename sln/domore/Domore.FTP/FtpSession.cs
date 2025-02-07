using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Domore {
    public sealed class FtpSession {
        private TcpClient Client;

        private Task Connect(CancellationToken token) {
            Client = new TcpClient
        }

        public Uri Uri { get; }
        public int Port { get; }

    }
}
