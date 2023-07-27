using System.Collections.Generic;

namespace Brows {
    public sealed class SSHConfig : EntryConfig {
        protected sealed override IEnumerable<string> DefaultKeyInit() {
            return new[] {
                nameof(SSHEntryData.Name),
                nameof(SSHEntryData.Permissions),
                nameof(SSHEntryData.OwnedByUser),
                nameof(SSHEntryData.OwnedByGroup),
                nameof(SSHEntryData.Length),
                nameof(SSHEntryData.LastWriteTime)
            };
        }

        protected sealed override IEnumerable<KeyValuePair<string, EntrySortDirection>> DefaultSortInit() {
            return new[] {
                KeyValuePair.Create(nameof(SSHEntryData.Kind), EntrySortDirection.Ascending),
                KeyValuePair.Create(nameof(SSHEntryData.Name), EntrySortDirection.Ascending)
            };
        }
    }
}
