using FilePath = System.IO.Path;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Describes a file or directory stored in a tar archive.
/// </summary>
public sealed class TarEntryInfo {
    private TarArchivePath Archive { get; }

    private TarEntryInfo(TarArchivePath archive, TarEntryName name, TarEntryKind kind) {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Archive = archive ?? throw new ArgumentNullException(nameof(archive));
        Kind = kind;
    }

    internal string Extension => field ??= (
        Kind == TarEntryKind.Path
            ? ""
            : FilePath.GetExtension(Name.Normalized));

    internal long? SizeOriginal { get; private init; }

    internal DateTimeOffset? LastWriteTime { get; private init; }

    internal static TarEntryInfo File(
        TarArchivePath archive,
        TarEntryName name,
        long sizeOriginal,
        DateTimeOffset? lastWriteTime) {
        return new TarEntryInfo(archive, name, TarEntryKind.File) {
            LastWriteTime = lastWriteTime,
            SizeOriginal = sizeOriginal,
        };
    }

    internal static TarEntryInfo Path(TarArchivePath archive, TarEntryName name) {
        return new TarEntryInfo(archive, name, TarEntryKind.Path);
    }

    internal Task Extract(string destination, bool overwrite, CancellationToken cancellationToken) {
        return Extract(
            map: new Dictionary<string, TarEntryInfo> {
                { destination, this }
            },
            overwrite: overwrite,
            progress: null,
            cancellationToken: cancellationToken);
    }

    internal static Task Extract(
        IEnumerable<KeyValuePair<string, TarEntryInfo>> map,
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
                info: new TarArchiveRead {
                    ExtractOverwrites = overwrite,
                    ExtractEntriesToFiles = group.ToDictionary(
                        item => item.EntryInfo.Name.Original,
                        item => item.FilePath)
                })));
    }

    /// <summary>
    /// Gets the name stored for the archive entry.
    /// </summary>
    public TarEntryName Name { get; }

    /// <summary>
    /// Gets whether the archive entry represents a file or directory.
    /// </summary>
    public TarEntryKind Kind { get; }
}
