using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Brows.Net.Sockets {
    internal class TcpReader : IDisposable {
        private TcpClient Client;
        private StreamReader StreamReader;

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                try {
                    StreamReader?.Dispose();
                }
                catch {
                }
                try {
                    Client?.Dispose();
                }
                catch {
                }
            }
        }

        protected virtual void OnStarted(EventArgs e) {
            Started?.Invoke(this, e);
        }

        protected virtual void OnStopped(EventArgs e) {
            Stopped?.Invoke(this, e);
        }

        protected virtual void OnError(TcpReadErrorEventArgs e) {
            Error?.Invoke(this, e);
        }

        public event EventHandler Started;
        public event EventHandler Stopped;
        public event TcpReadErrorEventHandler Error;

        public TcpListener Listener { get; }
        public Encoding Encoding { get; }

        public TcpReader(TcpListener listener, Encoding encoding) {
            Listener = listener ?? throw new ArgumentNullException(nameof(listener));
            Encoding = encoding;
        }

        public void Start() {
            Listener.Start();
            OnStarted(EventArgs.Empty);
        }

        public void Stop() {
            Listener.Stop();
            OnStopped(EventArgs.Empty);
        }

        public async IAsyncEnumerable<string> ReadClient([EnumeratorCancellation] CancellationToken cancellationToken) {
            var client = Client = await Listener.AcceptTcpClientAsync(cancellationToken);
            var stream = client.GetStream();
            var reader = StreamReader = new StreamReader(stream, Encoding);
            for (; ; ) {
                if (cancellationToken.IsCancellationRequested) {
                    break;
                }
                var s = default(string);
                var e = default(Exception);
                try {
                    s = await reader.ReadLineAsync();
                }
                catch (Exception ex) {
                    e = ex;
                }
                if (s != null) {
                    yield return s;
                }
                if (e != null) {
                    yield return null;
                    OnError(new TcpReadErrorEventArgs(e));
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
