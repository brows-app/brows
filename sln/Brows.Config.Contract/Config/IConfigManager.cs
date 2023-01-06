using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    public interface IConfigManager {
    }

    public interface IConfigManager<TConfig> : IConfigManager {
        TConfig Get();
        Task<TConfig> Load(CancellationToken cancellationToken);
    }
}
