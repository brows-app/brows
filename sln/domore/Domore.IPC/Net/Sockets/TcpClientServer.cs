using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DIRECTORY = System.IO.Directory;

namespace Domore.Net.Sockets {
    internal sealed class TcpClientServer {
        private Encoding Encoding => _Encoding ??= Encoding.UTF8;
        private Encoding _Encoding;

        private string PortPath(int? port = null) {
            return Path.Combine(Directory, nameof(TcpClientServer), nameof(port));
        }

        private async Task SavePort(int port, CancellationToken cancellationToken) {
            var path = PortPath(port);
            var directory = Path.GetDirectoryName(path);
            DIRECTORY.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, port.ToString(), cancellationToken).ConfigureAwait(false);
        }

        private async Task<int?> LoadPort(CancellationToken cancellationToken) {
            var path = PortPath();
            var text = default(string);
            try {
                text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (FileNotFoundException) {
                return null;
            }
            return int.Parse(text);
        }

        private async Task<IPEndPoint> LoadEndPoint(CancellationToken cancellationToken) {
            var port = await LoadPort(cancellationToken).ConfigureAwait(false);
            return port.HasValue
                ? new IPEndPoint(IPAddress.Loopback, port.Value)
                : null;
        }

        public string Directory { get; }

        public TcpClientServer(string directory) {
            Directory = directory;
        }

        public async Task<TcpWriter> ConnectWriter(CancellationToken cancellationToken) {
            var endPoint = await LoadEndPoint(cancellationToken).ConfigureAwait(false);
            if (endPoint == null) {
                return null;
            }
            var writer = new TcpWriter(endPoint, Encoding);
            await writer.Connect(cancellationToken).ConfigureAwait(false);
            return writer;
        }

        public async Task<TcpReader> StartReader(CancellationToken cancellationToken) {
            var reader = new TcpReader(Encoding);
            reader.Start();

            var port = reader.Port;
            await SavePort(port, cancellationToken).ConfigureAwait(false);
            return reader;
        }
    }
}
