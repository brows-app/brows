using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    using Threading.Tasks;

    internal class ConfigFileManager {
        public class Of<TConfig> : ConfigFileManager, IConfig<TConfig> where TConfig : class, new() {
            private TaskCache<TConfig> Cache =>
                _Cache ?? (
                _Cache = new(async cancellationToken => {
                    EventHandler invalidated = default;
                    var
                    info = await ConfigFileInfo.Load<TConfig>(cancellationToken);
                    info.Invalidated += invalidated = (s, e) => {
                        info.Invalidated -= invalidated;
                        _Cache = null;
                    };
                    return await info.Configure(cancellationToken);
                }));
            private TaskCache<TConfig> _Cache;

            public TConfig Loaded =>
                Cache.Result;

            public ValueTask<TConfig> Load(CancellationToken cancellationToken) {
                return Cache.Ready(cancellationToken);
            }
        }
    }
}
