using Brows.Data;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Brows;

internal sealed class FtpConfig : FileProtocolConfig {
    protected sealed override IEnumerable<string> DefaultKeyInit() {
        return new[] {
            nameof(FtpEntryData.Name),
            nameof(FtpEntryData.Time),
            nameof(FtpEntryData.Extension)
        };
    }

    protected sealed override IEnumerable<KeyValuePair<string, EntrySortDirection>> DefaultSortInit() {
        return new[] {
            KeyValuePair.Create(nameof(FtpEntryData.Kind), EntrySortDirection.Ascending),
            KeyValuePair.Create(nameof(FtpEntryData.Name), EntrySortDirection.Ascending)
        };
    }

    public ConcurrentDictionary<string, FtpClient> Client {
        get => field ??= [];
        set;
    }
}
