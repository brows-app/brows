using System.Collections.Generic;

namespace Brows {
    internal class ZipConfig : EntryConfig {
        protected override IEnumerable<string> DefaultKeyInit() {
            return new[] {
                nameof(ZipEntryData.RelativeName),
                nameof(ZipEntryData.SizeOriginal),
                nameof(ZipEntryData.SizeCompressed),
                nameof(ZipEntryData.LastWriteTime)
            };
        }

        protected override IEnumerable<KeyValuePair<string, EntrySortDirection>> DefaultSortInit() {
            return new[] {
                KeyValuePair.Create(nameof(ZipEntryData.Kind), EntrySortDirection.Descending)
            };
        }

        public EntryStreamGuiOptions Stream {
            get => _Stream ?? (_Stream = new());
            set => _Stream = value;
        }
        private EntryStreamGuiOptions _Stream;
    }
}
