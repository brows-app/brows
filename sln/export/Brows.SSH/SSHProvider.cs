using Brows.SSH;
using Domore.Logs;
using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using PATH = System.IO.Path;

namespace Brows {
    internal sealed class SSHProvider : Provider<SSHEntry, SSHConfig> {
        private static readonly ILog Log = Logging.For(typeof(SSHProvider));

        private SSHClient Client =>
            _Client ?? (
            _Client = SSHClient.Create(Uri, Password));
        private SSHClient _Client;

        protected sealed override void Dispose(bool disposing) {
            if (disposing) {
            }
            base.Dispose(disposing);
        }

        protected sealed override async Task Begin(CancellationToken token) {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(Begin), ID));
            }
            var list = Client.List(Path, token);
            await foreach (var info in list) {
                await Provide(new SSHEntry(this, info));
            }
        }

        protected sealed override async Task Refresh(CancellationToken token) {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(Refresh), ID));
            }
        }

        public bool CaseSensitive { get; }

        public sealed override string Parent {
            get {
                if (_Parent == null) {
                    var path = PATH.GetDirectoryName(Path)?.Replace('\\', '/');
                    if (path != null) {
                        _Parent = $"{Uri} {path}";
                    }
                }
                return _Parent;
            }
        }
        private string _Parent;

        public object Icon { get; }
        public string Path { get; }
        public Uri Uri { get; }
        public SecureString Password { get; }
        public SSHProviderFactory Factory { get; }

        public SSHProvider(SSHProviderFactory factory, Uri uri, SecureString password, string path, object icon) : base($"{uri?.ToString()?.TrimEnd('/')}>{path}") {
            Factory = factory;
            Uri = uri;
            Path = path;
            Icon = icon;
            Password = password;
        }
    }
}
