using System;
using System.IO;
using System.Linq;

namespace Brows;

internal static class ZipStreamPath {
    private static readonly char[] Separators = new[] { '\\', '/' };
    private static readonly char[] InvalidNameCharacters = new[] { '<', '>', ':', '"', '|', '?', '*' };

    private static void ValidateRelativePath(string relativePath) {
        var pathIsInvalid = string.IsNullOrEmpty(relativePath) ||
                            Path.IsPathRooted(relativePath) ||
                            relativePath[0] is '/' or '\\' ||
                            (relativePath.Length >= 2 && relativePath[1] == ':');
        if (pathIsInvalid) {
            throw new InvalidDataException("The ZIP entry path must be relative.");
        }

        var segments = relativePath.Split(Separators, StringSplitOptions.None);
        foreach (var segment in segments) {
            ValidateSegment(segment);
        }
    }

    private static void ValidateSegment(string segment) {
        var segmentIsInvalid = string.IsNullOrEmpty(segment) ||
                               segment is "." or ".." ||
                               segment.EndsWith('.') ||
                               segment.Any(character => char.IsControl(character) ||
                                                        InvalidNameCharacters.Contains(character)) ||
                               IsReservedDeviceName(segment);
        if (segmentIsInvalid) {
            throw new InvalidDataException("The ZIP entry contains an invalid Windows path component.");
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

    internal static string GetRelativePath(ZipEntry entry) {
        if (entry is null) {
            throw new ArgumentNullException(nameof(entry));
        }

        var originalName = entry.Info.Name.Original;
        var nameIsRooted = Path.IsPathRooted(originalName) ||
                           originalName.StartsWith('/') ||
                           originalName.StartsWith('\\');
        if (nameIsRooted) {
            throw new InvalidDataException("The ZIP entry path must be relative.");
        }

        var normalizedName = entry.Info.Name.Normalized;
        var providerPath = entry.Provider.Zip.RelativePath.Normalized;
        var prefix = string.IsNullOrEmpty(providerPath)
            ? ""
            : providerPath + Path.DirectorySeparatorChar;
        var entryIsInProvider = prefix == ""
            ? normalizedName != ""
            : normalizedName.StartsWith(prefix, StringComparison.Ordinal);
        if (entryIsInProvider is false) {
            throw new InvalidDataException("The ZIP entry is outside the current archive directory.");
        }

        var relativePath = prefix == ""
            ? normalizedName
            : normalizedName.Substring(prefix.Length);
        ValidateRelativePath(relativePath);
        return relativePath;
    }

}
