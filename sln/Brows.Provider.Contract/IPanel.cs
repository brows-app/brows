using System.Collections.Generic;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IPanel {
        int Column { get; }
        bool HasView(out IEntryDataView view);
        bool HasSelection(out IReadOnlySet<IEntry> entries);
        bool HasSelection<TEntry>(out IReadOnlySet<TEntry> entries) where TEntry : class, IEntry;
        bool HasEntry(out IEntry current);
        bool HasEntries(out IReadOnlyList<IEntry> entries);
        bool HasEntries<TEntry>(out IReadOnlyList<TEntry> entries) where TEntry : class, IEntry;
        bool HasProvider<TProvider>(out TProvider provider) where TProvider : class, IProvider;
        bool HasProviderService<TService>(out IProvider provider, out TService service) where TService : class, IProviderExport;
        Task<SecureString> GetSecret(string promptFormat, IEnumerable<string> promptArgs, CancellationToken token);
        Task<bool> Provide(string id, CancellationToken token);
    }
}
