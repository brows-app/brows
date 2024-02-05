using Brows.Data;
using Domore.Conf;
using System.Collections.Generic;
using CONVERT = System.Convert;

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

        public ScpConfig Scp {
            get => _Scp ?? (_Scp = new());
            set => _Scp = value;
        }
        private ScpConfig _Scp;

        public sealed class ScpConfig {
            [ConfConverter(typeof(ModeConverter))]
            public int Mode { get; set; } = 0777;

            private sealed class ModeConverter : ConfValueConverter {
                public sealed override object Convert(string value, ConfValueConverterState state) {
                    if (value?.Length > 1) {
                        if (value.StartsWith('0')) {
                            return CONVERT.ToInt32(value, fromBase: 8);
                        }
                    }
                    return base.Convert(value, state);
                }
            }
        }
    }
}
