using Brows.Entries;
using Brows.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading.Tasks;

namespace Brows.Panels;

public sealed class TestPanel : IPanel {
    public IReadOnlyList<IEntry> Entries =>
        Provider?.Observation?.Observed ?? Array.Empty<IEntry>();

    public IReadOnlySet<IEntry> Selection =>
        Provider?.Observation?.Selected ?? new HashSet<IEntry>();

    public object Window { get; set; }
    public IProvider Provider { get; set; }

    /// <summary>
    /// The column of the panel.
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// Services used by provider service lookups when <see cref="Provider"/>
    /// does not import a matching service itself.
    /// </summary>
    public IList<IProviderExport> Services { get; } = [];

    int IPanel.Column => Column;

    Task<SecureString> IPanel.GetSecret(string promptFormat, IEnumerable<string> promptArgs, CancellationToken token) {
        throw new NotImplementedException();
    }

    bool IPanel.HasEntry(out IEntry current) {
        current = Provider?.Observation?.Current();
        return current != null;
    }

    bool IPanel.HasEntries(out IReadOnlyList<IEntry> entries) {
        entries = Entries;
        return entries.Count > 0;
    }

    bool IPanel.HasEntries<TEntry>(out IReadOnlyList<TEntry> entries) {
        var e = Entries;
        if (e.Count == 0) {
            entries = null;
            return false;
        }
        entries = e.OfType<TEntry>().ToList();
        return entries.Count > 0;
    }

    bool IPanel.HasProvider(out IProvider provider) {
        provider = Provider;
        return provider != null;
    }

    bool IPanel.HasProvider<TProvider>(out TProvider provider) {
        provider = Provider as TProvider;
        return provider != null;
    }

    bool IPanel.HasProviderService<TService>(out IProvider provider, out TService service) {
        provider = Provider;
        service = provider?.Import<TService>() ?? Services.OfType<TService>().FirstOrDefault();
        return service != null;
    }

    bool IPanel.HasSelection(out IReadOnlySet<IEntry> entries) {
        entries = Selection;
        return entries.Count > 0;
    }

    bool IPanel.HasSelection<TEntry>(out IReadOnlySet<TEntry> entries) {
        var selection = Selection;
        if (selection.Count == 0) {
            entries = null;
            return false;
        }
        entries = selection.OfType<TEntry>().ToHashSet();
        return entries.Count > 0;
    }

    bool IPanel.HasView(out IEntryDataView view) {
        view = Provider?.Observation?.DataView;
        return view != null;
    }

    bool IPanel.HasWindow(out object native) {
        native = Window;
        return native is not null;
    }

    Task<bool> IPanel.Provide(string id, CancellationToken token) {
        throw new NotSupportedException();
    }
}
