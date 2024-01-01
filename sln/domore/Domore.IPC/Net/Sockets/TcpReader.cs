using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Domore.Net.Sockets {
    internal class TcpReader : IDisposable {
        private TcpListener Listener;

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                try {
                    Listener?.Stop();
                }
                catch {
                }
            }
        }

        public int Port =>
            ((IPEndPoint)Listener.LocalEndpoint).Port;

        public Encoding Encoding { get; }

        public TcpReader(Encoding encoding) {
            Listener = new TcpListener(new IPEndPoint(IPAddress.Loopback, 0));
            Encoding = encoding;
        }

        public void Start() {
            Listener.Start();
        }

        public async IAsyncEnumerable<string> ReadClient([EnumeratorCancellation] CancellationToken cancellationToken) {
            using (var client = await Listener.AcceptTcpClientAsync(cancellationToken)) {
                using (var stream = client.GetStream()) {
                    using (var reader = new StreamReader(stream, Encoding)) {
                        for (; ; ) {
                            if (cancellationToken.IsCancellationRequested) {
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                            yield return await reader.ReadLineAsync();
                        }
                    }
                }
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~TcpReader() {
            Dispose(false);
        }
    }
}
