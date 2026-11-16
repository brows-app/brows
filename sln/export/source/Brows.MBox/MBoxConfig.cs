using Brows.Data;
using Brows.Entries;
using Brows.MBox.Format;
using System;
using System.Collections.Generic;

namespace Brows;

/// <summary>
/// Holds the configuration of the mailbox provider.
/// </summary>
internal sealed class MBoxConfig : EntryConfig {
    internal event EventHandler MaxActivePreviewDecodesChanged;

    protected override IEnumerable<string> DefaultKeyInit() {
        return new[] {
            nameof(MBoxEntryData.Subject),
            nameof(MBoxEntryData.From),
            nameof(MBoxEntryData.Date),
            nameof(MBoxEntryData.Size),
        };
    }

    protected override IEnumerable<KeyValuePair<string, EntrySortDirection>> DefaultSortInit() {
        return new[] {
            KeyValuePair.Create(nameof(MBoxEntryData.Date), EntrySortDirection.Descending),
        };
    }

    /// <summary>
    /// Gets or sets the mailbox storage dialect that is used when a mailbox is read.
    /// </summary>
    public MBoxFormat Format { get; set; } = MBoxFormat.Mboxrd;

    /// <summary>
    /// Gets or sets the maximum number of messages that are indexed.
    /// </summary>
    public int MaxEntries { get; set; } = 1000000;

    /// <summary>
    /// Gets or sets the maximum number of bytes of metadata text that are retained for the
    /// indexed messages.
    /// </summary>
    public long MaxIndexTextBytes { get; set; } = 64 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of bytes of preview text that the preview cache retains.
    /// </summary>
    public long MaxPreviewCacheBytes { get; set; } = 32 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of completed previews that the preview cache retains.
    /// </summary>
    public int MaxPreviewCacheCount { get; set; } = 8;

    /// <summary>
    /// Gets or sets the maximum number of previews that are decoded at the same time.
    /// </summary>
    public int MaxActivePreviewDecodes {
        get;
        set {
            if (value < 1) {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxActivePreviewDecodes),
                    value,
                    "The maximum number of active preview decodes must be positive.");
            }
            var changed = field != value;
            field = value;
            if (changed) {
                MaxActivePreviewDecodesChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    } = 2;
}
