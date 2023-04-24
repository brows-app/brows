using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.Net.Sockets {
    internal class TcpWriter : IDisposable {
        private Stream Stream =>
            _Stream ?? (
            _Stream = Client.GetStream());
        private Stream _Stream;

        private StreamWriter StreamWriter =>
            _StreamWriter ?? (
            _StreamWriter = new StreamWriter(Stream, Encoding) { AutoFlush = false });
        private StreamWriter _StreamWriter;

        private TcpClient Client =>
            _Client ?? (
            _Client = new TcpClient());
        private TcpClient _Client;

        protected virtual void Dispose(bool disposing) {
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

        public async Task Connect(CancellationToken cancellationToken) {
            await Client.ConnectAsync(EndPoint, cancellationToken);
        }

        public async Task Write(string s, CancellationToken cancellationToken) {
            await StreamWriter.WriteLineAsync(s);
            await StreamWriter.FlushAsync();
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
