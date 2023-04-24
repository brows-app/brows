using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Data {
    using Exports;

    internal class FileSystemMetaData : EntryDataDefinition<FileSystemEntry, string> {
        public sealed override string Name =>
            Definition.Name;

        public sealed override string Group =>
            nameof(FileSystemMetaData);

        protected sealed override async Task<string> GetValue(FileSystemEntry entry, Action<string> progress, CancellationToken token) {
            if (entry != null) {
                var value = default(string);
                var ready = await entry.MetadataCache.Ready(token);
                if (ready.TryGetValue(Key, out value)) {
                    return value;
                }
                var refresh = await entry.MetadataCache.Refreshed(token);
                if (refresh.TryGetValue(Key, out value)) {
                    return value;
                }
            }
            return null;
        }

        protected sealed override void RefreshValue(FileSystemEntry entry) {
            if (entry != null) {
                if (entry.MetadataCache.Result != null) {
                    entry.MetadataCache.Refresh();
                }
            }
        }

        public IMetadataDefinition Definition { get; }

        public FileSystemMetaData(IMetadataDefinition definition) : base(definition?.Key) {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Width = 100;
        }

        public sealed override bool SuggestKey(ICommandContext context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) {
                return false;
            }
            if (active.HasProvider(out FileSystemProvider provider) == false) {
                return false;
            }
            return provider.SuggestKey(Key);
        }
    }
}
