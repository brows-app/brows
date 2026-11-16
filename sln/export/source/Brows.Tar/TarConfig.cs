using Brows.Entries;
using System.Collections.Generic;

namespace Brows;

internal sealed class TarConfig : EntryConfig {
    protected override IEnumerable<string> DefaultKeyInit() {
        return new[] {
            nameof(TarEntryData.RelativeName),
            nameof(TarEntryData.SizeOriginal),
            nameof(TarEntryData.LastWriteTime)
        };
    }

    protected override IEnumerable<KeyValuePair<string, EntrySortDirection>> DefaultSortInit() {
        return new[] {
            KeyValuePair.Create(nameof(TarEntryData.Kind), EntrySortDirection.Descending)
        };
    }

    public EntryStreamGuiOptions Stream {
        get => field ??= new();
        set;
    }
}
