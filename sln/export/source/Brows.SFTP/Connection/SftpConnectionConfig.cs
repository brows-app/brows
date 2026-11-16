using Brows.Data;
using Brows.Entries;
using Domore.Conf;
using Domore.Conf.Converters;
using System.Collections.Generic;

namespace Brows;

internal sealed class SftpConnectionConfig : EntryConfig {
    protected sealed override IEnumerable<string> DefaultKeyInit() {
        return [nameof(SftpConnectionEntryData.Kind), nameof(SftpConnectionEntryData.Data)];
    }

    protected sealed override IEnumerable<KeyValuePair<string, EntrySortDirection>> DefaultSortInit() {
        return [KeyValuePair.Create(nameof(SftpConnectionEntryData.Order), EntrySortDirection.Ascending)];
    }

    public Dictionary<string, PrivateKeyOption> PrivateKey {
        get => field ??= new(System.StringComparer.OrdinalIgnoreCase);
        set;
    }

    internal sealed class PrivateKeyOption {
        [ConfListItems]
        public List<string> File {
            get => field ??= [];
            set;
        }
    }
}
