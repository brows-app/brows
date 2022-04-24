using System;

namespace Brows.Net.Sockets {
    internal delegate void TcpReadErrorEventHandler(object sender, TcpReadErrorEventArgs e);

    internal class TcpReadErrorEventArgs : EventArgs {
        public Exception Exception { get; }

        public TcpReadErrorEventArgs(Exception exception) {
            Exception = exception;
        }
    }
}
