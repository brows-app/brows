using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.Net.Sockets {
    internal sealed class TcpWriter : IDisposable {
        private Stream Stream => _Stream ??= Client.GetStream();
        private Stream _Stream;

        private StreamWriter StreamWriter => _StreamWriter ??= new StreamWriter(Stream, Encoding) { AutoFlush = false };
        private StreamWriter _StreamWriter;

        private TcpClient Client => _Client ??= new TcpClient();
        private TcpClient _Client;

        private void Dispose(bool disposing) {
            if (disposing) {
                try {
                    _Stream?.Dispose();
                }
                catch {
                }
                try {
                    _StreamWriter?.Dispose();
                }
                catch {
                }
                try {
                    _Client?.Dispose();
                }
                catch {
                }
            }
        }

        public bool Connected =>
            Client.Connected;

        public IPEndPoint EndPoint { get; }
        public Encoding Encoding { get; }

        public TcpWriter(IPEndPoint endPoint, Encoding encoding) {
            EndPoint = endPoint;
            Encoding = encoding;
        }

        public ValueTask Connect(CancellationToken cancellationToken) {
            return Client.ConnectAsync(EndPoint, cancellationToken);
        }

        public async Task Write(string s, CancellationToken cancellationToken) {
            await StreamWriter.WriteLineAsync(s).ConfigureAwait(false);
            await StreamWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~TcpWriter() {
            Dispose(false);
        }
    }
}
