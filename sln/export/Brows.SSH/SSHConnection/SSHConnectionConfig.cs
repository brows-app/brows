using System.Collections.Generic;

namespace Brows.SSHConnection {
    public sealed class SSHConnectionConfig : EntryConfig {
        protected sealed override IEnumerable<string> DefaultKeyInit() {
            return new[] {
                nameof(SSHConnectionEntryData.Kind),
            };
        }

        protected sealed override IEnumerable<KeyValuePair<string, EntrySortDirection>> DefaultSortInit() {
            return new[] {
                KeyValuePair.Create(nameof(SSHConnectionEntryData.Order), EntrySortDirection.Ascending)
            };
        }
    }
}
