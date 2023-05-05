using Brows.Data;
using System;
using System.Collections.Generic;

namespace Brows {
    public sealed class FileSystemConfig : EntryConfig {
        protected sealed override IEnumerable<string> DefaultKeyInit() {
            return new[] {
                nameof(FileSystemEntryData.Name),
                nameof(FileSystemInfoData.Length),
                nameof(FileSystemInfoData.LastWriteTime),
                nameof(FileSystemEntryData.Extension)
            };
        }

        protected sealed override IEnumerable<KeyValuePair<string, EntrySortDirection>> DefaultSortInit() {
            return new[] {
                KeyValuePair.Create(nameof(FileSystemEntryData.Kind), EntrySortDirection.Ascending),
                KeyValuePair.Create(nameof(FileSystemEntryData.Name), EntrySortDirection.Ascending)
            };
        }

        public int ProvideDelay { get; set; } = 50;

        public Dictionary<string, string> Editor {
            get => _Editor ?? (_Editor = new(StringComparer.OrdinalIgnoreCase));
            set => _Editor = value;
        }
        private Dictionary<string, string> _Editor;

        public EntryStreamGuiOptions Stream {
            get => _Stream ?? (_Stream = new());
            set => _Stream = value;
        }
        private EntryStreamGuiOptions _Stream;

        public FileSystemEventConfig FileSystemEvent {
            get => _FileSystemEvent ?? (_FileSystemEvent = new());
            set => _FileSystemEvent = value;
        }
        private FileSystemEventConfig _FileSystemEvent;

        public class FileSystemEventConfig {
            public int RefreshDelay { get; set; } = 250;
        }
    }
}
