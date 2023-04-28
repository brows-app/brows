using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PATH = System.IO.Path;

namespace Brows {
    public sealed class ZipEntryInfo {
        private ZipEntryInfo(ZipArchivePath archive, ZipEntryName name, ZipEntryKind kind) {
            Kind = kind;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Archive = archive ?? throw new ArgumentNullException(nameof(archive));
        }

        internal ZipArchivePath Archive { get; }

        internal static ZipEntryInfo File(ZipArchivePath archive, ZipEntryName name, ZipArchiveEntry zipArchiveEntry) {
            if (null == zipArchiveEntry) throw new ArgumentNullException(nameof(zipArchiveEntry));
            return new ZipEntryInfo(archive, name, ZipEntryKind.File) {
                Attributes = zipArchiveEntry.ExternalAttributes,
                CRC32 = zipArchiveEntry.Crc32,
                LastWriteTime = zipArchiveEntry.LastWriteTime,
                SizeCompressed = zipArchiveEntry.CompressedLength,
                SizeOriginal = zipArchiveEntry.Length,
            };
        }

        internal static ZipEntryInfo Path(ZipArchivePath archive, ZipEntryName name) {
            return new ZipEntryInfo(archive, name, ZipEntryKind.Path);
        }

        public string Extension =>
            _Extension ?? (
            _Extension = Kind == ZipEntryKind.Path
                ? ""
                : PATH.GetExtension(Name.Normalized));
        private string _Extension;

        public ZipEntryName Name { get; }
        public ZipEntryKind Kind { get; }

        public long? SizeCompressed { get; private init; }
        public long? SizeOriginal { get; private init; }
        public long? CRC32 { get; private init; }
        public int? Attributes { get; private init; }
        public DateTimeOffset? LastWriteTime { get; private init; }

        public async Task Extract(string destination, bool overwrite, CancellationToken cancellationToken) {
            await Extract(
                map: new Dictionary<string, ZipEntryInfo> {
                    { destination, this }
                },
                progress: null,
                overwrite: overwrite,
                cancellationToken: cancellationToken);
        }

        public static async Task Extract(IEnumerable<KeyValuePair<string, ZipEntryInfo>> map, bool overwrite, IOperationProgress progress, CancellationToken cancellationToken) {
            if (null == map) throw new ArgumentNullException(nameof(map));
            await Task.WhenAll(map
                .Select(pair => new {
                    FilePath = pair.Key,
                    EntryInfo = pair.Value
                })
                .GroupBy(item => item.EntryInfo.Archive)
                .Select(group => group.Key.Read(
                    progress: progress,
                    cancellationToken: cancellationToken,
                    info: new() {
                        ExtractOverwrites = overwrite,
                        ExtractEntriesToFiles = group.ToDictionary(
                            item => item.EntryInfo.Name.Original,
                            item => item.FilePath)
                    })));
        }
    }
}
