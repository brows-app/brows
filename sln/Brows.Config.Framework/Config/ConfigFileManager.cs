using Domore.Logs;
using Domore.Threading.Tasks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    internal abstract class ConfigFileManager {
        private static readonly ILog Log = Logging.For(typeof(ConfigFileManager));

        private ConfigFileManager() {
        }

        public event EventHandler Changed;

        public sealed class Of<TConfig> : ConfigFileManager, IConfig<TConfig> where TConfig : class, new() {
            private TaskCache<TConfig> Cache {
                get => _Cache ??= new(async token => {
                    var info = await ConfigFileInfo.Load<TConfig>(token: token, invalidated: (s, e) => {
                        if (Log.Info()) {
                            Log.Info($"invalidated > {typeof(TConfig).Name}");
                        }
                        Cache = null;
                        Changed?.Invoke(this, EventArgs.Empty);
                    });
                    return await info.Configure(token);
                });
                set => _Cache = value;
            }
            private TaskCache<TConfig> _Cache;

            public TConfig Loaded =>
                Cache.Result;

            public Task<TConfig> Load(CancellationToken token) {
                if (Log.Info()) {
                    Log.Info($"{nameof(Load)} > {typeof(TConfig).Name}");
                }
                return Cache.Ready(token);
            }
        }
    }
}
