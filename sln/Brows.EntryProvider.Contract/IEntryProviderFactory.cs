using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IEntryProviderFactory {
        Task<IEntryProvider> CreateFor(string id, CancellationToken cancellationToken);
        Task<IEntryProvider> CreateDefault(CancellationToken cancellationToken);
    }
}
