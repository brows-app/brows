using Domore.Conf;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FILE = System.IO.File;

namespace Brows.Config {
    using Threading.Tasks;

    internal class ConfigFileInfo {
        private static readonly ILog Log = Logging.For(typeof(ConfigFileInfo));
        private static readonly Dictionary<string, object> Locker = new();

        private ConfigFileInfo(string file, string @default) {
            File = file;
            Default = @default;
        }

        private async Task<TConfig> Configure<TConfig>(CancellationToken cancellationToken) where TConfig : new() {
            var file = File;
            if (Locker.TryGetValue(file, out var locker) == false) {
                Locker[file] = locker = new();
            }
            return await Async.With(cancellationToken).Run(() => {
                lock (locker) {
                    file = FILE.Exists(file)
                        ? file
                        : Default;
                    return Conf.Contain(file).Configure(new TConfig(), key: "");
                }
            });
        }

        public event EventHandler Invalidated;

        public bool Invalid { get; private set; }
        public string File { get; }
        public string Default { get; }

        public void Invalidate() {
            if (Log.Info()) {
                Log.Info(nameof(Invalidate) + " > " + File);
            }
            Invalid = true;
            Invalidated?.Invoke(this, EventArgs.Empty);
        }

        public static async Task<For<TConfig>> Load<TConfig>(CancellationToken cancellationToken) where TConfig : new() {
            var dir = await ConfigPath.FileReady(cancellationToken);
            var type = typeof(TConfig).Name.ToLowerInvariant();
            var name = type.EndsWith("config") ? type.Substring(0, type.Length - "config".Length) : type;
            var file = Path.Combine(dir, $"{name}.conf");
            var dflt = Path.Combine(dir, "default", $"{name}.conf.default");
            var loaded = new For<TConfig>(file, dflt);
            await ConfigFileWatcher.Subscribe(loaded, cancellationToken);
            return loaded;
        }

        public class For<TConfig> : ConfigFileInfo where TConfig : new() {
            public For(string file, string @default) : base(file, @default) {
            }

            public Task<TConfig> Configure(CancellationToken cancellationToken) {
                return Configure<TConfig>(cancellationToken);
            }
        }
    }
}
