using Brows.Exports;
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Data {
    internal sealed class FileSystemMetaData : EntryDataDefinition<FileSystemEntry, FileSystemMetaData.MetadataValue> {
        public sealed override string Name =>
            Definition.Name;

        public sealed override string Group =>
            nameof(FileSystemMetaData);

        protected sealed override async Task<FileSystemMetaData.MetadataValue> GetValue(FileSystemEntry entry, Action<FileSystemMetaData.MetadataValue> progress, CancellationToken token) {
            if (entry != null) {
                var value = default(IMetadataValue);
                var ready = await entry.MetadataCache.Ready(token).ConfigureAwait(false);
                if (ready.TryGetValue(Key, out value)) {
                    return new MetadataValue(value);
                }
                var refresh = await entry.MetadataCache.Refreshed(token).ConfigureAwait(false);
                if (refresh.TryGetValue(Key, out value)) {
                    return new MetadataValue(value);
                }
            }
            return null;
        }

        protected sealed override Task RefreshValue(FileSystemEntry entry, CancellationToken token) {
            return
                token.IsCancellationRequested ? Task.FromCanceled(token) :
                entry?.MetadataCache?.Result != null ? entry.MetadataCache.Refresh(token) :
                Task.CompletedTask;
        }

        public IMetadataDefinition Definition { get; }

        public FileSystemMetaData(IMetadataDefinition definition) : base(definition?.Key) {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Converter = new MetadataValue.Converter();
            Width = 100;
        }

        public sealed override async Task<bool> SuggestKey(ICommandContext context, CancellationToken token) {
            if (null == context) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == context.HasPalette(out var palette)) return false;
            if (false == active.HasProvider(out FileSystemProvider provider)) return false;
            var entries = provider.Provided.ToList();
            foreach (var entry in entries) {
                var file = await provider.Factory.Metadata.Load(entry.Info.FullName, palette, token).ConfigureAwait(false);
                if (file.Keys.Contains(Definition.Key)) {
                    return true;
                }
            }
            return false;
        }

        public sealed class MetadataValue : IComparable {
            public IMetadataValue Agent { get; }

            public MetadataValue(IMetadataValue agent) {
                Agent = agent ?? throw new ArgumentNullException(nameof(agent));
            }

            public sealed class Converter : EntryDataConverter {
                public sealed override string Convert(object value, object parameter, CultureInfo culture) {
                    var v = value as MetadataValue;
                    if (v != null) {
                        return v.Agent.Display;
                    }
                    return null;
                }
            }

            int IComparable.CompareTo(object obj) {
                return obj is MetadataValue other
                    ? Comparer.Default.Compare(Agent.Object, other.Agent.Object)
                    : Comparer.Default.Compare(this, obj);
            }
        }
    }
}
