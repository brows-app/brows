using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Brows.Commands;

internal sealed record SevenZipInput {
    public bool CaseSensitive { get; }
    public IReadOnlyList<FileSystemInfo> FileSystemInfo { get; }

    public SevenZipInput(bool caseSensitive, IReadOnlyList<FileSystemInfo> fileSystemInfo) {
        CaseSensitive = caseSensitive;
        FileSystemInfo = fileSystemInfo ?? throw new ArgumentNullException(nameof(fileSystemInfo));
    }

    public IEnumerable<SevenZipEntry> Entries(IOperationProgress progress, CancellationToken token) {
        IEnumerable<FileSystemInfo> entries(FileSystemInfo info) {
            switch (info) {
                case FileInfo f:
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    if (progress is not null) {
                        progress.Change(data: f.Name);
                    }
                    yield return f;
                    break;
                case DirectoryInfo d:
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    if (progress is not null) {
                        progress.Change(data: d.Name);
                    }
                    var descendants = d.EnumerateFileSystemInfos("*", SearchOption.AllDirectories).ToList();
                    if (descendants.Count == 0) {
                        yield return d;
                        break;
                    }
                    foreach (var descendant in descendants) {
                        if (token.IsCancellationRequested) {
                            token.ThrowIfCancellationRequested();
                        }
                        var include = descendant is FileInfo;
                        if (descendant is DirectoryInfo directory) {
                            include = directory.EnumerateFileSystemInfos().Any() == false;
                        }
                        if (include == false) {
                            continue;
                        }
                        if (progress is not null) {
                            progress.Change(data: descendant.Name);
                        }
                        yield return descendant;
                    }
                    break;
            }
        }
        var items = FileSystemInfo.SelectMany(entries).ToList();
        var common = FileSystemPath.SkipCommonOf(
            items.Select(item => item.FullName),
            CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        return common
            .Select(item => new SevenZipEntry {
                EntryName = item.RelativePath
                    .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/'),
                FilePath = item.OriginalPath
            });
    }
}
