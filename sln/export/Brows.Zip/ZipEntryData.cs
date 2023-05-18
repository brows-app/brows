using Brows.Exports;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal static class ZipEntryData {
        public abstract class ZipEntryDataDefinition<T> : EntryDataDefinition<ZipEntry, T> {
        }

        public sealed class RelativeName : ZipEntryDataDefinition<string> {
            protected sealed override Task<string> GetValue(ZipEntry entry, Action<string> progress, CancellationToken cancellationToken) {
                return Task.FromResult(entry.Info.Name.Top);
            }

            public RelativeName() {
                Width = 250;
            }
        }

        public sealed class Kind : ZipEntryDataDefinition<ZipEntryKind> {
            protected override Task<ZipEntryKind> GetValue(ZipEntry entry, Action<ZipEntryKind> progress, CancellationToken cancellationToken) {
                return Task.FromResult(entry.Info.Kind);
            }

            public sealed override Task<bool> SuggestKey(ICommandContext context, CancellationToken token) {
                return Task.FromResult(false);
            }
        }

        public sealed class Icon : ZipEntryDataDefinition<object> {
            protected sealed override async Task<object> GetValue(ZipEntry entry, Action<object> progress, CancellationToken cancellationToken) {
                var service = IconZipEntryInfo;
                if (service != null) {
                    return await service.Icon(entry.Info, cancellationToken);
                }
                return null;
            }

            public IIconZipEntryInfo IconZipEntryInfo { get; set; }

            public sealed override Task<bool> SuggestKey(ICommandContext context, CancellationToken token) {
                return Task.FromResult(false);
            }
        }

        public sealed class SizeCompressed : ZipEntryDataDefinition<long?> {
            protected sealed override async Task<long?> GetValue(ZipEntry entry, Action<long?> progress, CancellationToken cancellationToken) {
                return await Task.FromResult(entry.Info.SizeCompressed);
            }

            public SizeCompressed() {
                Width = 100;
                Alignment = EntryDataAlignment.Right;
                Converter = EntryDataConverter.FileSystemSize;
            }
        }

        public sealed class SizeOriginal : ZipEntryDataDefinition<long?> {
            protected sealed override async Task<long?> GetValue(ZipEntry entry, Action<long?> progress, CancellationToken cancellationToken) {
                return await Task.FromResult(entry.Info.SizeOriginal);
            }

            public SizeOriginal() {
                Width = 100;
                Alignment = EntryDataAlignment.Right;
                Converter = EntryDataConverter.FileSystemSize;
            }
        }

        public sealed class CRC32 : ZipEntryDataDefinition<long?> {
            protected sealed override Task<long?> GetValue(ZipEntry entry, Action<long?> progress, CancellationToken cancellationToken) {
                return Task.FromResult(entry.Info.CRC32);
            }
        }

        public sealed class Attributes : ZipEntryDataDefinition<int?> {
            protected sealed override Task<int?> GetValue(ZipEntry entry, Action<int?> progress, CancellationToken cancellationToken) {
                return Task.FromResult(entry.Info.Attributes);
            }
        }

        public sealed class LastWriteTime : ZipEntryDataDefinition<DateTime?> {
            protected override Task<DateTime?> GetValue(ZipEntry entry, Action<DateTime?> progress, CancellationToken cancellationToken) {
                return Task.FromResult(entry.Info.LastWriteTime?.LocalDateTime);
            }

            public LastWriteTime() {
                Width = 150;
                Converter = EntryDataConverter.DateTime;
            }
        }
    }
}
