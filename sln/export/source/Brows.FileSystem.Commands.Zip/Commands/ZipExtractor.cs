using Brows.IO.Compression;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Commands;

internal static class ZipExtractor {
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static bool IsContainedPathCollision(string root, string path, HashSet<string> fileTargets) {
        if (fileTargets.Contains(path)) {
            return true;
        }

        var parentPath = Path.GetDirectoryName(path);
        while (parentPath is not null) {
            var parentIsRoot = string.Equals(parentPath, root, PathComparison);
            if (parentIsRoot) {
                break;
            }
            if (fileTargets.Contains(parentPath)) {
                return true;
            }
            parentPath = Path.GetDirectoryName(parentPath);
        }
        return false;
    }

    private static string RelativePath(ZipArchiveEntry entry, out bool isDirectory) {
        isDirectory = entry.IsDirectory();
        var originalPath = entry.FullName;
        var path = isDirectory
            ? originalPath[..^1]
            : originalPath;
        FileSystemOutputPath.ValidateRelativePath(path);
        return path;
    }

    internal static async Task ExtractAsync(
        string archiveFileName,
        string destinationDirectoryName,
        bool overwrite,
        IOperationProgress progress,
        CancellationToken token) {
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectoryName);

        progress?.Change(kind: OperationProgressKind.FileSize);

        var root = Path.GetFullPath(destinationDirectoryName);
        using var archive = ZipFile.OpenRead(archiveFileName);
        var entries = new List<(ZipArchiveEntry Entry, string RelativePath, string Target, bool IsDirectory)>();
        var fileTargets = new HashSet<string>(PathComparer);
        var directoryTargets = new Dictionary<string, string>(PathComparer);
        foreach (var entry in archive.Entries) {
            token.ThrowIfCancellationRequested();
            var relativePath = RelativePath(entry, out var isDirectory);
            var target = FileSystemOutputPath.GetPath(root, relativePath);
            entries.Add((entry, relativePath, target, isDirectory));
            if (isDirectory) {
                directoryTargets[target] = relativePath;
                continue;
            }

            fileTargets.Add(target);
            var parent = Path.GetDirectoryName(target);
            while (parent is not null) {
                var parentIsRoot = string.Equals(parent, root, PathComparison);
                if (parentIsRoot) {
                    break;
                }
                var parentRelativePath = Path.GetRelativePath(root, parent);
                directoryTargets[parent] = parentRelativePath;
                parent = Path.GetDirectoryName(parent);
            }
        }

        var fileCollidesWithDirectory = directoryTargets.Keys.Any(path =>
            IsContainedPathCollision(root, path, fileTargets));
        if (fileCollidesWithDirectory) {
            throw new InvalidDataException("A ZIP file entry collides with another entry's output path.");
        }

        Directory.CreateDirectory(root);
        foreach (var directory in directoryTargets.OrderBy(item => item.Key.Length)) {
            token.ThrowIfCancellationRequested();
            FileSystemOutputPath.CreateDirectory(root, directory.Value);
        }

        foreach (var item in entries) {
            if (item.IsDirectory) {
                continue;
            }

            token.ThrowIfCancellationRequested();
            FileSystemOutputPath.EnsureNoReparsePoints(root, item.Target);
            await ZipEntryExtraction.ExtractAsync(item.Entry, item.Target, overwrite, progress, token);
        }
    }
}
