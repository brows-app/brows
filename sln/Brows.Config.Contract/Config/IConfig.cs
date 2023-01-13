using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    public interface IConfig {
    }

    public interface IConfig<TConfig> : IConfig {
        TConfig Loaded { get; }
        ValueTask<TConfig> Load(CancellationToken cancellationToken);
    }
}
