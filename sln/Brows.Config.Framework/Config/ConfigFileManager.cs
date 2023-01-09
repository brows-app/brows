using Domore.Conf;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    using Threading.Tasks;

    internal class ConfigFileManager {
        private static readonly object ConfFile = new();

        public class Of<TConfig> : ConfigFileManager, IConfig<TConfig> where TConfig : new() {
            public TConfig Loaded { get; private set; }

            public async Task<TConfig> Load(CancellationToken cancellationToken) {
                if (Loaded == null) {
                    Loaded = await Async.With(cancellationToken).Run(() => {
                        cancellationToken.ThrowIfCancellationRequested();
                        lock (ConfFile) {
                            cancellationToken.ThrowIfCancellationRequested();
                            return Conf.Configure(new TConfig());
                        }
                    });
                }
                return Loaded;
            }
        }
    }
}
