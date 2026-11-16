using Brows.Entries;
using System.Collections.Generic;

namespace Brows;

internal sealed record TarArchiveUpdate {
    internal IEnumerable<string> DeleteEntries {
        get => field ??= new List<string>();
        init;
    }

    internal IEnumerable<TarEntryInfo> DeleteSelectedEntries {
        get => field ??= new List<TarEntryInfo>();
        init;
    }

    internal Dictionary<string, string> CreateEntriesFromFiles {
        get => field ??= new();
        init;
    }

    internal IEnumerable<IEntryStreamSet> CopyStreamSets {
        get => field ??= new List<IEntryStreamSet>();
        init;
    }

    internal string CopyStreamTargetDirectory { get; init; }
}
