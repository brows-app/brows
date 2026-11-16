using System.Collections.Generic;

namespace Brows;

internal sealed class GitStatusConfig : EntryConfig {
    protected sealed override IEnumerable<string> DefaultKeyInit() => [
        nameof(GitStatusEntryData.Path),
        nameof(GitStatusEntryData.Staged),
        nameof(GitStatusEntryData.WorkTree),
        nameof(GitStatusEntryData.OriginalPath)
    ];

    protected sealed override IEnumerable<KeyValuePair<string, EntrySortDirection>> DefaultSortInit() => [
        KeyValuePair.Create(nameof(GitStatusEntryData.Conflict), EntrySortDirection.Descending),
        KeyValuePair.Create(nameof(GitStatusEntryData.Path), EntrySortDirection.Ascending)
    ];
}
