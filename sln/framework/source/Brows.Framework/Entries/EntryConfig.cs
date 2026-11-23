using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Entries;

/// <summary>
/// Configures entry columns, sorting, collection updates, and refresh timing.
/// </summary>
public class EntryConfig {
    /// <summary>
    /// Supplies default column keys when no explicit value has been configured.
    /// </summary>
    /// <returns>
    /// The initial column keys in display order.
    /// </returns>
    protected virtual IEnumerable<string> DefaultKeyInit() {
        return Array.Empty<string>();
    }

    /// <summary>
    /// Supplies default sorting when no explicit value has been configured.
    /// </summary>
    /// <returns>
    /// The initial sort keys and directions.
    /// </returns>
    protected virtual IEnumerable<KeyValuePair<string, EntrySortDirection>> DefaultSortInit() {
        return Array.Empty<KeyValuePair<string, EntrySortDirection>>();
    }

    /// <summary>
    /// Gets or sets whether entry grids automatically size columns to their data and available space.
    /// The default is true. When false, column widths can still be fitted explicitly.
    /// </summary>
    public bool AutoFit { get; set; } = true;

    /// <summary>
    /// Gets the configured default column keys in display order.
    /// </summary>
    public IEnumerable<string> DefaultKeys =>
        DefaultKey.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Gets the configured default sort keys and directions, using the last direction for repeated keys.
    /// </summary>
    public IEnumerable<KeyValuePair<string, EntrySortDirection>> DefaultSorting =>
        DefaultSort
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s =>
                s.EndsWith('<') ? KeyValuePair.Create(s[..^1], EntrySortDirection.Ascending) :
                s.EndsWith('>') ? KeyValuePair.Create(s[..^1], EntrySortDirection.Descending) :
                KeyValuePair.Create(s, EntrySortDirection.Ascending))
            .GroupBy(pair => pair.Key)
            .Select(group => KeyValuePair.Create(group.Key, group.Last().Value));

    /// <summary>
    /// Gets or sets default column keys separated by vertical bars.
    /// </summary>
    public string DefaultKey {
        get => field ??= string.Join("|", DefaultKeyInit());
        set;
    }

    /// <summary>
    /// Gets or sets sort keys separated by vertical bars. A trailing &lt; means ascending, &gt; means descending, and
    /// no suffix means ascending.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the settings for adding and removing entries from the view.
    /// </summary>
    public ObservationConfig Observe {
        get => field ??= new();
        set;
    }

    /// <summary>
    /// Gets or sets the timing settings for postponed provider refreshes.
    /// </summary>
    public PostponedRefreshConfig PostponedRefresh {
        get => field ??= new();
        set;
    }

    /// <summary>
    /// Configures how entries are added to and removed from the view.
    /// </summary>
    public class ObservationConfig {
        /// <summary>
        /// Gets or sets the batch settings for adding entries.
        /// </summary>
        public ChunkConfig Add {
            get => field ??= new();
            set;
        }

        /// <summary>
        /// Gets or sets the batch settings for removing entries.
        /// </summary>
        public ChunkConfig Remove {
            get => field ??= new();
            set;
        }
    }

    /// <summary>
    /// Controls the batch size and delay used for entry collection updates.
    /// </summary>
    public class ChunkConfig {
        /// <summary>
        /// Gets or sets the entries per batch. Values below one process the supplied entries in a single batch.
        /// </summary>
        public int Size { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the delay between updates in milliseconds. Nonpositive values disable the delay.
        /// </summary>
        public int Delay { get; set; } = 10;
    }

    /// <summary>
    /// Configures the interval between postponed refreshes.
    /// </summary>
    public class PostponedRefreshConfig {
        /// <summary>
        /// Gets or sets the minimum interval before another postponed refresh.
        /// </summary>
        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(2.5);
    }
}
