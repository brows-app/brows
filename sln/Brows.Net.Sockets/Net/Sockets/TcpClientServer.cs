using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Net.Sockets {
    using Data;
    using IO;

    internal class TcpClientServer {
        private Encoding Encoding =>
            _Encoding ?? (
            _Encoding = Encoding.UTF8);
        private Encoding _Encoding;

        private string PortPath(int? port = null) {
            return Path.Combine(DataPath.Root, Name, nameof(port));
        }

        private async Task SavePort(int port, CancellationToken cancellationToken) {
            var path = PortPath(port);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            await File.WriteAllTextAsync(path, port.ToString(), cancellationToken);
        }

        private async Task<int?> LoadPort(CancellationToken cancellationToken) {
            var path = PortPath();
            var exists = await FileAsync.Exists(path, cancellationToken);
            if (exists == false) return null;
            var text = await File.ReadAllTextAsync(path, cancellationToken);
            return int.Parse(text);
        }

        private async Task<IPEndPoint> LoadEndPoint(CancellationToken cancellationToken) {
            var port = await LoadPort(cancellationToken);
            return port.HasValue
                ? new IPEndPoint(IPAddress.Loopback, port.Value)
                : null;
        }

        public string Name { get; }

        public TcpClientServer(string name) {
            Name = name;
        }

        public async Task<TcpWriter> CreateWriter(CancellationToken cancellationToken) {
            var endPoint = await LoadEndPoint(cancellationToken);
            return endPoint == null
                ? null
                : new TcpWriter(endPoint, Encoding);
        }

        public Task<TcpReader> CreateReader(CancellationToken cancellationToken) {
            var listener = new TcpListener(new IPEndPoint(IPAddress.Loopback, 0));
            var
            reader = new TcpReader(listener, Encoding);
            reader.Started += (s, e) => {
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                SavePort(port, cancellationToken).ContinueWith(_ => {
                });
            };
            return Task.FromResult(reader);
        }
    }
}
