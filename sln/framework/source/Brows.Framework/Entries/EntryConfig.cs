using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Entries;

public class EntryConfig {
    protected virtual IEnumerable<string> DefaultKeyInit() {
        return Array.Empty<string>();
    }

    protected virtual IEnumerable<KeyValuePair<string, EntrySortDirection>> DefaultSortInit() {
        return Array.Empty<KeyValuePair<string, EntrySortDirection>>();
    }

    public IEnumerable<string> DefaultKeys =>
        DefaultKey.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public IEnumerable<KeyValuePair<string, EntrySortDirection>> DefaultSorting =>
        DefaultSort
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s =>
                s.EndsWith('<') ? KeyValuePair.Create(s[..^1], EntrySortDirection.Ascending) :
                s.EndsWith('>') ? KeyValuePair.Create(s[..^1], EntrySortDirection.Descending) :
                KeyValuePair.Create(s, EntrySortDirection.Ascending))
            .GroupBy(pair => pair.Key)
            .Select(group => KeyValuePair.Create(group.Key, group.Last().Value));

    public string DefaultKey {
        get => field ??= string.Join("|", DefaultKeyInit());
        set;
    }

    public string DefaultSort {
        get => field ??= string.Join("|", DefaultSortInit()
            .Select(pair => $"{pair.Key}{(
                pair.Value == EntrySortDirection.Ascending
                    ? "<"
                    : pair.Value == EntrySortDirection.Descending
                        ? ">"
                        : "")}"));
        set;
    }

    public ObservationConfig Observe {
        get => field ??= new();
        set;
    }

    public PostponedRefreshConfig PostponedRefresh {
        get => field ??= new();
        set;
    }

    public class ObservationConfig {
        public ChunkConfig Add {
            get => field ??= new();
            set;
        }

        public ChunkConfig Remove {
            get => field ??= new();
            set;
        }
    }

    public class ChunkConfig {
        public int Size { get; set; } = 1000;
        public int Delay { get; set; } = 10;
    }

    public class PostponedRefreshConfig {
        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(2.5);
    }
}
