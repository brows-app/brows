using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IPanel {
        int Column { get; }
        bool HasView(out IEntryDataView view);
        bool HasSelection(out IReadOnlySet<IEntry> entries);
        bool HasSelection<TEntry>(out IReadOnlySet<TEntry> entries) where TEntry : class, IEntry;
        bool HasEntries(out IReadOnlyList<IEntry> entries);
        bool HasEntries<TEntry>(out IReadOnlyList<TEntry> entries) where TEntry : class, IEntry;
        bool HasProvider<TProvider>(out TProvider provider) where TProvider : class, IEntryProvider;
        bool HasProviderService<TService>(out IEntryProvider provider, out TService service) where TService : class, IEntryProviderExport;
        Task<bool> Provide(string id, CancellationToken token);
    }
}
