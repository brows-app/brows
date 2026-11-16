using Brows.Data;
using Brows.Entries;
using System.Collections.Generic;

namespace Brows;

internal sealed class SftpConfig : FileProtocolConfig {
    protected sealed override IEnumerable<string> DefaultKeyInit() {
        return [
            nameof(SftpEntryData.Name),
            nameof(SftpEntryData.Kind),
            nameof(SftpEntryData.Length),
            nameof(SftpEntryData.Permissions),
            nameof(SftpEntryData.Owner),
            nameof(SftpEntryData.Group),
            nameof(SftpEntryData.LastWriteTime)
        ];
    }

    protected sealed override IEnumerable<KeyValuePair<string, EntrySortDirection>> DefaultSortInit() {
        return [
            KeyValuePair.Create(nameof(SftpEntryData.Kind), EntrySortDirection.Ascending),
            KeyValuePair.Create(nameof(SftpEntryData.Name), EntrySortDirection.Ascending)
        ];
    }

}
