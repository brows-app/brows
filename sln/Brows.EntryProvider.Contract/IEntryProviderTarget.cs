using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IEntryProviderTarget : IEntryBrowser {
        Task Add(IReadOnlyCollection<IEntry> entries, CancellationToken cancellationToken);
        Task Remove(IReadOnlyCollection<IEntry> entries, CancellationToken cancellationToken);
    }
}
