using System;
using System.IO;
using System.Linq;

namespace Brows;

internal static class FileSystemOutputPath {
    private static readonly char[] Separators = new[] {
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar
    }.Distinct().ToArray();

    private static readonly char[] InvalidNameCharacters = new[] {
        '<', '>', ':', '"', '|', '?', '*'
    };

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static void EnsureContained(string root, string path) {
        var rootWithSeparator = Path.EndsInDirectorySeparator(root)
            ? root
            : root + Path.DirectorySeparatorChar;
        var isRoot = string.Equals(root, path, PathComparison);
        var isContained = path.StartsWith(rootWithSeparator, PathComparison);
        if (isRoot is false && isContained is false) {
            throw new InvalidDataException("The output path is outside the selected directory.");
        }
    }

    private static void ValidateSegment(string segment) {
        var isInvalid = string.IsNullOrEmpty(segment) ||
                        segment is "." or ".." ||
                        segment.EndsWith('.') ||
                        segment.EndsWith(' ') ||
                        segment.Any(character => char.IsControl(character) ||
                                                 InvalidNameCharacters.Contains(character)) ||
                        IsReservedDeviceName(segment);
        if (isInvalid) {
            throw new InvalidDataException("The output path contains an invalid Windows path component.");
        }
    }

    private static bool IsReservedDeviceName(string segment) {
        var baseName = segment.Split('.')[0].TrimEnd(' ', '.');
        var isReserved = baseName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
                         baseName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                         baseName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
                         baseName.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
                         baseName.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) ||
                         baseName.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase) ||
                         IsNumberedDeviceName(baseName, "COM") ||
                         IsNumberedDeviceName(baseName, "LPT");
        return isReserved;
    }

    private static bool IsNumberedDeviceName(string name, string prefix) {
        var hasPrefix = name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        if (hasPrefix is false) {
            return false;
        }
        var suffix = name.Substring(prefix.Length);
        var hasAsciiNumber = suffix.Length == 1 && suffix[0] is >= '1' and <= '9';
        var hasSuperscriptNumber = suffix is "¹" or "²" or "³";
        return hasAsciiNumber || hasSuperscriptNumber;
    }

    internal static string GetPath(string root, string relativePath) {
        if (root is null) {
            throw new ArgumentNullException(nameof(root));
        }
        ValidateRelativePath(relativePath);
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        EnsureContained(fullRoot, fullPath);
        return fullPath;
    }

    internal static void ValidateRelativePath(string relativePath) {
        var pathIsInvalid = string.IsNullOrEmpty(relativePath) ||
                            Path.IsPathRooted(relativePath) ||
                            relativePath[0] is '/' or '\\' ||
                            (relativePath.Length >= 2 && relativePath[1] == ':');
        if (pathIsInvalid) {
            throw new InvalidDataException("The output path must be relative.");
        }
        var pathHasTrailingSeparator = Separators.Contains(relativePath[^1]);
        var pathToValidate = pathHasTrailingSeparator
            ? relativePath[..^1]
            : relativePath;
        var segments = pathToValidate.Split(Separators, StringSplitOptions.None);
        foreach (var segment in segments) {
            ValidateSegment(segment);
        }
    }

    internal static string CreateDirectory(string root, string relativePath) {
        var path = GetPath(root, relativePath);
        EnsureNoReparsePoints(root, path);
        Directory.CreateDirectory(path);
        EnsureNoReparsePoints(root, path);
        return path;
    }

    internal static (string Path, FileStream Stream) CreateStagingFile(string root, string relativePath) {
        var destinationPath = GetPath(root, relativePath);
        EnsureNoReparsePoints(root, destinationPath);
        var parentPath = Path.GetDirectoryName(destinationPath);
        if (parentPath is null) {
            throw new InvalidDataException("The output path has no parent directory.");
        }

        EnsureNoReparsePoints(root, parentPath);
        Directory.CreateDirectory(parentPath);
        EnsureNoReparsePoints(root, parentPath);

        for (; ; ) {
            var stagingPath = Path.Combine(parentPath, $".brows-{Guid.NewGuid():N}.tmp");
            var stagingRelativePath = Path.GetRelativePath(Path.GetFullPath(root), stagingPath);
            stagingPath = GetPath(root, stagingRelativePath);
            EnsureNoReparsePoints(root, stagingPath);
            try {
                var stream = new FileStream(
                    stagingPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                return (stagingPath, stream);
            }
            catch (IOException) {
                var stagingPathExists = File.Exists(stagingPath) || Directory.Exists(stagingPath);
                if (stagingPathExists is false) {
                    throw;
                }
                EnsureNoReparsePoints(root, stagingPath);
            }
        }
    }

    internal static string PublishStagingFile(string root, string relativePath, string stagingPath) {
        var extension = Path.GetExtension(relativePath);
        var destinationRelativePath = relativePath;
        for (; ; ) {
            var destinationPath = GetPath(root, destinationRelativePath);
            EnsureNoReparsePoints(root, destinationPath);
            var parentPath = Path.GetDirectoryName(destinationPath);
            if (parentPath is null) {
                throw new InvalidDataException("The output path has no parent directory.");
            }
            EnsureNoReparsePoints(root, parentPath);
            try {
                File.Move(stagingPath, destinationPath, overwrite: false);
                return destinationRelativePath;
            }
            catch (IOException) {
                var destinationPathExists = File.Exists(destinationPath) || Directory.Exists(destinationPath);
                if (destinationPathExists is false) {
                    throw;
                }
                EnsureNoReparsePoints(root, destinationPath);
                destinationRelativePath = destinationRelativePath + ".copy" + extension;
            }
        }
    }

    internal static void DeleteStagingFile(string root, string stagingPath) {
        var fullRoot = Path.GetFullPath(root);
        var fullStagingPath = Path.GetFullPath(stagingPath);
        EnsureContained(fullRoot, fullStagingPath);
        EnsureNoReparsePoints(fullRoot, fullStagingPath);
        if (File.Exists(fullStagingPath)) {
            File.Delete(fullStagingPath);
        }
    }

    internal static void EnsureNoReparsePoints(string root, string path) {
        if (root is null) {
            throw new ArgumentNullException(nameof(root));
        }
        if (path is null) {
            throw new ArgumentNullException(nameof(path));
        }

        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(path);
        EnsureContained(fullRoot, fullPath);
        var relativePath = Path.GetRelativePath(fullRoot, fullPath);
        if (relativePath == ".") {
            return;
        }

        var currentPath = fullRoot;
        var segments = relativePath.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments) {
            currentPath = Path.Combine(currentPath, segment);
            FileAttributes attributes;
            try {
                attributes = File.GetAttributes(currentPath);
            }
            catch (FileNotFoundException) {
                break;
            }
            catch (DirectoryNotFoundException) {
                break;
            }

            var isReparsePoint = attributes.HasFlag(FileAttributes.ReparsePoint);
            if (isReparsePoint) {
                throw new IOException("The output path traverses a reparse point.");
            }
        }
    }

}
