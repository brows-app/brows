using Brows.Exports;
using System;
using System.Threading.Tasks;

namespace Brows;

internal static class SevenZipEntryData {
    public abstract class SevenZipEntryDataDefinition<T> : EntryDataExport<SevenZipEntry, T> {
    }

    public sealed class RelativeName : SevenZipEntryDataDefinition<string> {
        protected sealed override Task<string> GetValue(
            SevenZipEntry entry,
            Action<string> progress,
            CancellationToken cancellationToken) {
            return Task.FromResult(entry.Info.Name.Top);
        }

        public RelativeName() {
            Width = 250;
        }
    }

    public sealed class Kind : SevenZipEntryDataDefinition<SevenZipEntryKind> {
        protected override Task<SevenZipEntryKind> GetValue(
            SevenZipEntry entry,
            Action<SevenZipEntryKind> progress,
            CancellationToken cancellationToken) {
            return Task.FromResult(entry.Info.Kind);
        }

        public sealed override Task<bool> SuggestKey(ICommandContext context, CancellationToken token) {
            return Task.FromResult(false);
        }
    }

    public sealed class Icon : SevenZipEntryDataDefinition<object> {
        protected sealed override async Task<object> GetValue(
            SevenZipEntry entry,
            Action<object> progress,
            CancellationToken cancellationToken) {
            var service = IconSevenZipEntryInfo;
            if (service is not null) {
                return await service.Icon(entry.Info, cancellationToken);
            }
            return null;
        }

        public IIconSevenZipEntryInfo IconSevenZipEntryInfo { get; set; }

        public sealed override Task<bool> SuggestKey(ICommandContext context, CancellationToken token) {
            return Task.FromResult(false);
        }
    }

    public sealed class SizeCompressed : SevenZipEntryDataDefinition<long?> {
        protected sealed override async Task<long?> GetValue(
            SevenZipEntry entry,
            Action<long?> progress,
            CancellationToken cancellationToken) {
            return await Task.FromResult(entry.Info.SizeCompressed);
        }

        public SizeCompressed() {
            Width = 100;
            Alignment = EntryDataAlignment.Right;
            Converter = EntryDataConverter.FileSystemSize;
        }
    }

    public sealed class SizeOriginal : SevenZipEntryDataDefinition<long?> {
        protected sealed override async Task<long?> GetValue(
            SevenZipEntry entry,
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

    public sealed class CRC32 : SevenZipEntryDataDefinition<long?> {
        protected sealed override Task<long?> GetValue(
            SevenZipEntry entry,
            Action<long?> progress,
            CancellationToken cancellationToken) {
            return Task.FromResult(entry.Info.CRC32);
        }
    }

    public sealed class Attributes : SevenZipEntryDataDefinition<int?> {
        protected sealed override Task<int?> GetValue(
            SevenZipEntry entry,
            Action<int?> progress,
            CancellationToken cancellationToken) {
            return Task.FromResult(entry.Info.Attributes);
        }
    }

    public sealed class LastWriteTime : SevenZipEntryDataDefinition<DateTime?> {
        protected override Task<DateTime?> GetValue(
            SevenZipEntry entry,
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
