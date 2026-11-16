using Brows.SevenZip;
using FilePath = System.IO.Path;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Describes a file or directory stored in a 7-Zip archive.
/// </summary>
public sealed class SevenZipEntryInfo {
    private SevenZipArchivePath Archive { get; }

    private SevenZipEntryInfo(SevenZipArchivePath archive, SevenZipEntryName name, SevenZipEntryKind kind) {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Archive = archive ?? throw new ArgumentNullException(nameof(archive));
        Kind = kind;
    }

    internal string Extension => field ??= (
        Kind == SevenZipEntryKind.Path
            ? ""
            : FilePath.GetExtension(Name.Normalized));

    internal long? SizeCompressed { get; private init; }

    internal long? SizeOriginal { get; private init; }

    internal long? CRC32 { get; private init; }

    internal int? Attributes { get; private init; }

    internal DateTimeOffset? LastWriteTime { get; private init; }

    internal static SevenZipEntryInfo File(
        SevenZipArchivePath archive,
        SevenZipEntryName name,
        SevenZipArchiveEntry entry) {
        ArgumentNullException.ThrowIfNull(entry);
        return new SevenZipEntryInfo(archive, name, SevenZipEntryKind.File) {
            Attributes = (int)entry.Attributes,
            CRC32 = entry.Crc,
            LastWriteTime = entry.ModifiedTime,
            SizeCompressed = entry.PackedSize,
            SizeOriginal = entry.Size,
        };
    }

    internal static SevenZipEntryInfo Path(SevenZipArchivePath archive, SevenZipEntryName name) {
        return new SevenZipEntryInfo(archive, name, SevenZipEntryKind.Path);
    }

    internal Task Extract(string destination, bool overwrite, CancellationToken cancellationToken) {
        return Extract(
            map: new Dictionary<string, SevenZipEntryInfo> {
                { destination, this }
            },
            overwrite: overwrite,
            progress: null,
            cancellationToken: cancellationToken);
    }

    internal static Task Extract(
        IEnumerable<KeyValuePair<string, SevenZipEntryInfo>> map,
        bool overwrite,
        IOperationProgress progress,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(map);
        return Task.WhenAll(map
            .Select(pair => new {
                FilePath = pair.Key,
                EntryInfo = pair.Value
            })
            .GroupBy(item => item.EntryInfo.Archive)
            .Select(group => group.Key.Read(
                progress: progress,
                token: cancellationToken,
                info: new SevenZipArchiveRead {
                    ExtractOverwrites = overwrite,
                    ExtractEntriesToFiles = group.ToDictionary(
                        item => item.EntryInfo.Name.Original,
                        item => item.FilePath)
                })));
    }

    /// <summary>
    /// Gets the name stored for the archive entry.
    /// </summary>
    public SevenZipEntryName Name { get; }

    /// <summary>
    /// Gets whether the archive entry represents a file or directory.
    /// </summary>
    public SevenZipEntryKind Kind { get; }
}
