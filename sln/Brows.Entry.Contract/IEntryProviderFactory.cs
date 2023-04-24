using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IEntryProviderFactory : IExport {
        Task<IEntryProvider> CreateFor(string id, IPanel panel, CancellationToken cancellationToken);
    }
}
