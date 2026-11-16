using Brows.Entries;
using System.Collections.Generic;

namespace Brows;

internal sealed class SevenZipConfig : EntryConfig {
    protected override IEnumerable<string> DefaultKeyInit() {
        return new[] {
            nameof(SevenZipEntryData.RelativeName),
            nameof(SevenZipEntryData.SizeOriginal),
            nameof(SevenZipEntryData.SizeCompressed),
            nameof(SevenZipEntryData.LastWriteTime)
        };
    }

    protected override IEnumerable<KeyValuePair<string, EntrySortDirection>> DefaultSortInit() {
        return new[] {
            KeyValuePair.Create(nameof(SevenZipEntryData.Kind), EntrySortDirection.Descending)
        };
    }

    public EntryStreamGuiOptions Stream {
        get => field ??= new();
        set;
    }
}
