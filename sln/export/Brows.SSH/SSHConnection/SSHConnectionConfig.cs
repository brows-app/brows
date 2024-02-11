using Domore.Conf.Converters;
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

        public Dictionary<string, PrivateKeyOption> PrivateKey {
            get => _PrivateKey ??= [];
            set => _PrivateKey = value;
        }
        private Dictionary<string, PrivateKeyOption> _PrivateKey;

        public sealed class PrivateKeyOption {
            [ConfListItems]
            public List<string> File {
                get => _File ??= [];
                set => _File = value;
            }
            private List<string> _File;
        }
    }
}
