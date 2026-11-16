using Brows.Exports;
using System;
using System.Threading.Tasks;

namespace Brows;

internal static class TarEntryData {
    public abstract class TarEntryDataDefinition<T> : EntryDataExport<TarEntry, T> {
    }

    public sealed class RelativeName : TarEntryDataDefinition<string> {
        protected sealed override Task<string> GetValue(
            TarEntry entry,
            Action<string> progress,
            CancellationToken cancellationToken) {
            return Task.FromResult(entry.Info.Name.Top);
        }

        public RelativeName() {
            Width = 250;
        }
    }

    public sealed class Kind : TarEntryDataDefinition<TarEntryKind> {
        protected override Task<TarEntryKind> GetValue(
            TarEntry entry,
            Action<TarEntryKind> progress,
            CancellationToken cancellationToken) {
            return Task.FromResult(entry.Info.Kind);
        }

        public sealed override Task<bool> SuggestKey(ICommandContext context, CancellationToken token) {
            return Task.FromResult(false);
        }
    }

    public sealed class Icon : TarEntryDataDefinition<object> {
        protected sealed override async Task<object> GetValue(
            TarEntry entry,
            Action<object> progress,
            CancellationToken cancellationToken) {
            var service = IconTarEntryInfo;
            if (service is not null) {
                return await service.Icon(entry.Info, cancellationToken);
            }
            return null;
        }

        public IIconTarEntryInfo IconTarEntryInfo { get; set; }

        public sealed override Task<bool> SuggestKey(ICommandContext context, CancellationToken token) {
            return Task.FromResult(false);
        }
    }

    public sealed class SizeOriginal : TarEntryDataDefinition<long?> {
        protected sealed override async Task<long?> GetValue(
            TarEntry entry,
            Action<long?> progress,
            CancellationToken cancellationToken) {
            return await Task.FromResult(entry.Info.SizeOriginal);
        }

        public SizeOriginal() {
            Width = 100;
            Alignment = EntryDataAlignment.Right;
            Converter = EntryDataConverter.FileSystemSize;
        }
    }

    public sealed class LastWriteTime : TarEntryDataDefinition<DateTime?> {
        protected override Task<DateTime?> GetValue(
            TarEntry entry,
            Action<DateTime?> progress,
            CancellationToken cancellationToken) {
            return Task.FromResult(entry.Info.LastWriteTime?.LocalDateTime);
        }

        public LastWriteTime() {
            Width = 150;
            Converter = EntryDataConverter.DateTime;
        }
    }
}
