using Brows.SSH;
using Domore.Logs;
using Domore.Threading.Tasks;
using System;
using System.Threading.Tasks;
using PATH = System.IO.Path;

namespace Brows {
    internal abstract class SSHProviderBase<TEntry, TConfig> : Provider<TEntry, TConfig> where TEntry : Entry where TConfig : EntryConfig, new() {
        private static readonly ILog Log = Logging.For(typeof(SSHProviderBase<TEntry, TConfig>));

        protected TaskCache<SSHClient> Client =>
            _Client ?? (
            _Client = new TaskCache<SSHClient>(async token => {
                var
                client = await Factory.Client(Uri, token);
                client.Providing();
                return client;
            }));
        private TaskCache<SSHClient> _Client;

        protected sealed override async void End() {
            try {
                var client = Client.Result;
                if (client != null) {
                    await Task.Delay(5000);
                    var clientReferences = client.Released();
                    if (clientReferences == 0) {
                        await using (client) {
                        }
                    }
                }
            }
            catch (Exception ex) {
                if (Log.Error()) {
                    Log.Error(ex);
                }
            }
        }

        public bool CaseSensitive { get; }

        public sealed override string Parent {
            get {
                if (_Parent == null) {
                    var path = PATH.GetDirectoryName(Uri.AbsolutePath)?.Replace('\\', '/');
                    if (path != null) {
                        var
                        parentUri = new UriBuilder(Uri);
                        parentUri.Path = path;
                        _Parent = parentUri.ToString();
                    }
                }
                return _Parent;
            }
        }
        private string _Parent;

        public Uri Uri { get; }
        public object Icon { get; }
        public SSHProviderFactory Factory { get; }

        public SSHProviderBase(SSHProviderFactory factory, Uri uri, object icon) : base(uri?.ToString()) {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));
            Icon = icon;
        }
    }
}
