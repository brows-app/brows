using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Brows.Commands;

internal sealed class ZipInput {
    private static string GetPathRoot(string originalPath, string relativePath) {
        var relativeParts = relativePath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        var root = Path.GetFullPath(originalPath);
        for (var index = 0; index < relativeParts.Length; index++) {
            var parent = Path.GetDirectoryName(root);
            root = parent ?? Path.GetPathRoot(root) ?? root;
        }
        return root;
    }

    public bool CaseSensitive { get; }
    public IReadOnlyList<FileSystemInfo> FileSystemInfo { get; }

    public ZipInput(bool caseSensitive, IReadOnlyList<FileSystemInfo> fileSystemInfo) {
        CaseSensitive = caseSensitive;
        FileSystemInfo = fileSystemInfo ?? throw new ArgumentNullException(nameof(fileSystemInfo));
    }

    public IEnumerable<ZipEntry> Entries(IOperationProgress progress, CancellationToken token) {
        token.ThrowIfCancellationRequested();
        if (FileSystemInfo.Count == 0) {
            return Array.Empty<ZipEntry>();
        }
        IEnumerable<FileSystemInfo> entries(FileSystemInfo info) {
            switch (info) {
                case FileInfo f:
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    if (progress != null) {
                        progress.Change(data: f.Name);
                    }
                    yield return f;
                    break;
                case DirectoryInfo d:
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    if (progress != null) {
                        progress.Change(data: d.Name);
                    }
                    var descendants = new List<FileSystemInfo>();
                    foreach (var descendant in d.EnumerateFileSystemInfos("*", SearchOption.AllDirectories)) {
                        token.ThrowIfCancellationRequested();
                        descendants.Add(descendant);
                    }
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
                            include = true;
                            foreach (var child in directory.EnumerateFileSystemInfos()) {
                                token.ThrowIfCancellationRequested();
                                include = false;
                                break;
                            }
                        }
                        if (include == false) {
                            continue;
                        }
                        if (progress != null) {
                            progress.Change(data: descendant.Name);
                        }
                        yield return descendant;
                    }
                    break;
            }
        }
        var comparer = CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var selectedDirectory = FileSystemInfo.Count == 1
            ? FileSystemInfo[0] as DirectoryInfo
            : null;
        var root = selectedDirectory?.FullName;
        if (root is null) {
            var firstSelection = FileSystemPath.SkipCommonOf(
                FileSystemInfo.Select(info => info.FullName),
                comparer).First();
            root = GetPathRoot(firstSelection.OriginalPath, firstSelection.RelativePath);
        }
        var items = FileSystemInfo.SelectMany(entries).ToList();
        return items.Select(item => {
            token.ThrowIfCancellationRequested();
            var fullPath = item.FullName;
            var relativePath = Path.GetRelativePath(root, fullPath);
            var selectedRootIsDirectoryMarker = item is DirectoryInfo && relativePath == ".";
            if (selectedRootIsDirectoryMarker) {
                relativePath = item.Name;
            }
            var relativePathEscapesRoot = relativePath == ".." ||
                relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                Path.IsPathRooted(relativePath);
            if (relativePathEscapesRoot) {
                throw new InvalidOperationException("Compression inputs must share the selected archive root.");
            }
            return new ZipEntry {
                EntryName = relativePath
                    .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/'),
                FilePath = fullPath,
                IsDirectory = item is DirectoryInfo
            };
        });
    }
}
