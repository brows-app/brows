using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Brows;

internal static class LocalFileMove {
    private static List<Item> Snapshot(IReadOnlyList<string> sources, CancellationToken token) {
        var items = new List<Item>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string path) {
            token.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(path);
            var isNewPath = visited.Add(fullPath);
            if (!isNewPath) {
                throw new InvalidDataException("The local move selection contains overlapping paths.");
            }
            var attributes = File.GetAttributes(fullPath);
            var isReparsePoint = (attributes & FileAttributes.ReparsePoint) != 0;
            if (isReparsePoint) {
                throw new NotSupportedException($"Moves of local reparse points are not supported: {fullPath}");
            }
            var isDirectory = (attributes & FileAttributes.Directory) != 0;
            if (isDirectory) {
                var children = Directory.EnumerateFileSystemEntries(fullPath)
                    .OrderBy(child => child, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                foreach (var child in children) {
                    Add(child);
                }
                var directory = new DirectoryInfo(fullPath);
                items.Add(new Item(fullPath, true, 0, directory.LastWriteTimeUtc));
                return;
            }
            var file = new FileInfo(fullPath);
            items.Add(new Item(fullPath, false, file.Length, file.LastWriteTimeUtc));
        }

        foreach (var source in sources) {
            Add(source);
        }
        return items;
    }

    private static bool SameSnapshot(IReadOnlyList<Item> first, IReadOnlyList<Item> second) {
        var firstItems = first.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase);
        var secondItems = second.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase);
        return firstItems.SequenceEqual(secondItems);
    }

    private static void Delete(IReadOnlyList<Item> items, CancellationToken token) {
        foreach (var item in items) {
            token.ThrowIfCancellationRequested();
            var attributes = File.GetAttributes(item.Path);
            var isReparsePoint = (attributes & FileAttributes.ReparsePoint) != 0;
            if (isReparsePoint) {
                throw new NotSupportedException($"Moves of local reparse points are not supported: {item.Path}");
            }
            var isDirectory = (attributes & FileAttributes.Directory) != 0;
            var changedType = isDirectory != item.IsDirectory;
            if (changedType) {
                throw new IOException("The local move source changed during the move.");
            }
            if (isDirectory) {
                Directory.Delete(item.Path, recursive: false);
                continue;
            }
            var file = new FileInfo(item.Path);
            var changedFile = file.Length != item.Length || file.LastWriteTimeUtc != item.LastWriteTimeUtc;
            if (changedFile) {
                throw new IOException("The local move source changed during the move.");
            }
            File.Delete(item.Path);
        }
    }

    internal sealed record Item(string Path, bool IsDirectory, long Length, DateTime LastWriteTimeUtc);

    internal static void DeleteUnchanged(
        IReadOnlyList<string> sources,
        IReadOnlyList<Item> originalSnapshot,
        string changedMessage,
        CancellationToken token) {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(originalSnapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(changedMessage);
        token.ThrowIfCancellationRequested();
        var currentSnapshot = Snapshot(sources, token);
        var unchanged = SameSnapshot(originalSnapshot, currentSnapshot);
        if (!unchanged) {
            throw new IOException(changedMessage);
        }
        Delete(originalSnapshot, token);
    }

    internal static IReadOnlyList<Item> Capture(IReadOnlyList<string> sources, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(sources);
        token.ThrowIfCancellationRequested();
        return Snapshot(sources, token);
    }
}
