using Domore.Conf;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    internal abstract class ConfigFileInfo {
        private static readonly ILog Log = Logging.For(typeof(ConfigFileInfo));
        private static readonly Dictionary<string, object> Locker = new(StringComparer.OrdinalIgnoreCase);

        private event EventHandler Invalidated;

        private ConfigFileInfo(string file, string @default, string special) {
            File = file;
            Default = @default;
            Special = special;
        }

        private Task<TConfig> Configure<TConfig>(CancellationToken token) where TConfig : new() {
            var file = File;
            if (Locker.TryGetValue(file, out var locker) == false) {
                Locker[file] = locker = new();
            }
            return Task.Run(cancellationToken: token, function: () => {
                lock (locker) {
                    TConfig config(TConfig target, string file) {
                        for (var i = 0; ; i++) {
                            var err = default(Exception);
                            var conf = Conf.Contain(file, special: Special);
                            try {
                                return conf.Configure(target, key: "");
                            }
                            catch (IOException ex) {
                                err = ex;
                            }
                            if (i > 3) {
                                if (Log.Error()) {
                                    Log.Error(err);
                                }
                                if (Log.Warn()) {
                                    Log.Warn($"Unable to configure > {typeof(TConfig).Name}");
                                }
                                return target;
                            }
                            if (Log.Info()) {
                                Log.Info(err?.Message);
                            }
                            Thread.Sleep(250);
                        }
                    }
                    var
                    target = new TConfig();
                    target = config(target, Default);
                    target = config(target, file);
                    return target;
                }
            });
        }

        public bool Invalid { get; private set; }
        public string File { get; }
        public string Default { get; }
        public string Special { get; }

        public void Invalidate() {
            if (Log.Info()) {
                Log.Info(nameof(Invalidate) + " > " + File);
            }
            Invalid = true;
            Invalidated?.Invoke(this, EventArgs.Empty);
        }

        public static async Task<For<TConfig>> Load<TConfig>(EventHandler invalidated, CancellationToken token) where TConfig : new() {
            var dir = await ConfigPath.FileReady(token).ConfigureAwait(false);
            var type = typeof(TConfig).Name.ToLowerInvariant();
            var name = type.EndsWith("config") ? type.Substring(0, type.Length - "config".Length) : type;
            var file = Path.Combine(dir, $"{name}.conf");
            var dflt = Path.Combine(dir, "default", $"{name}.conf.default");
            var
            loaded = new For<TConfig>(file, dflt, "brows");
            loaded.Invalidated += invalidated;
            await ConfigFileWatcher.Subscribe(loaded, token).ConfigureAwait(false);
            return loaded;
        }

        public sealed class For<TConfig> : ConfigFileInfo where TConfig : new() {
            public For(string file, string @default, string special) : base(file, @default, special) {
            }

            public Task<TConfig> Configure(CancellationToken token) {
                return Configure<TConfig>(token);
            }
        }
    }
}
