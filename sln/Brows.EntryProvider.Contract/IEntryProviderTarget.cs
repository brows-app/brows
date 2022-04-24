using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IEntryProviderTarget : IEntryBrowser {
        Task Add(IEntry entry, CancellationToken cancellationToken);
        Task Remove(IEntry entry, CancellationToken cancellationToken);
    }
}
