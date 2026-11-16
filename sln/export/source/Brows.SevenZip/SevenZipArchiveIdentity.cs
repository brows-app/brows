using Brows.Composition;
using Brows.SevenZip;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class SevenZipArchiveIdentity : IEquatable<SevenZipArchiveIdentity> {
    private StringComparer Comparer => IsCaseSensitive
        ? StringComparer.Ordinal
        : StringComparer.OrdinalIgnoreCase;

    private string Kind => IsCaseSensitive ? "sensitive" : "insensitive";

    private SevenZipArchiveIdentity(string fullName, bool isCaseSensitive) {
        FullName = fullName ?? throw new ArgumentNullException(nameof(fullName));
        IsCaseSensitive = isCaseSensitive;
    }

    private static string ResolveStoredFilePath(string path, CancellationToken token) {
        token.ThrowIfCancellationRequested();
        var fullName = Path.GetFullPath(path);
        var exists = File.Exists(fullName);
        return exists
            ? ResolveStoredPath(fullName, token)
            : ResolveStoredParentPath(fullName, token);
    }

    private static async Task<string> ResolveStoredFilePathAsync(
        string path,
        ICaseSensitiveDirectory caseSensitiveDirectory,
        CancellationToken token) {
        token.ThrowIfCancellationRequested();
        var fullName = Path.GetFullPath(path);
        var exists = await Task.Run(() => File.Exists(fullName), token);
        var directory = exists ? fullName : Path.GetDirectoryName(fullName);
        var fileName = exists ? null : Path.GetFileName(fullName);
        var storedPath = await ResolveStoredPathAsync(directory, caseSensitiveDirectory, token);
        return exists ? storedPath : Path.Combine(storedPath, fileName);
    }

    private static async Task<string> ResolveStoredPathAsync(
        string path,
        ICaseSensitiveDirectory caseSensitiveDirectory,
        CancellationToken token) {
        token.ThrowIfCancellationRequested();
        var fullName = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullName);
        if (string.IsNullOrEmpty(root)) {
            return fullName;
        }
        root = NormalizeRoot(root);
        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        var components = fullName[root.Length..]
            .Split(separators, StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        foreach (var component in components) {
            token.ThrowIfCancellationRequested();
            var currentIsDirectory = await Task.Run(() => Directory.Exists(current), token);
            if (currentIsDirectory is false) {
                current = Path.Combine(current, component);
                continue;
            }
            var currentIsCaseSensitive = await GetCaseSensitivityAsync(current, caseSensitiveDirectory, token);
            try {
                var names = await Task.Run(() => Directory.EnumerateFileSystemEntries(current)
                    .Select(Path.GetFileName)
                    .Where(name => string.Equals(name, component, StringComparison.OrdinalIgnoreCase))
                    .ToArray(), token);
                var exact = names.FirstOrDefault(name => string.Equals(name, component, StringComparison.Ordinal));
                var hasUniqueInsensitiveMatch = currentIsCaseSensitive is false && names.Length == 1;
                var selected = exact ?? (hasUniqueInsensitiveMatch ? names[0] : component);
                current = Path.Combine(current, selected);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception) {
                current = Path.Combine(current, component);
            }
        }
        return current;
    }

    private static async Task<bool> GetCaseSensitivityAsync(
        string directory,
        ICaseSensitiveDirectory caseSensitiveDirectory,
        CancellationToken token) {
        var existingDirectory = await Task.Run(() => FindExistingDirectory(directory, token), token);
        if (existingDirectory is null) {
            return false;
        }
        return await GetExistingDirectoryCaseSensitivityAsync(existingDirectory, caseSensitiveDirectory, token);
    }

    private static async Task<bool> GetExistingDirectoryCaseSensitivityAsync(
        string directory,
        ICaseSensitiveDirectory caseSensitiveDirectory,
        CancellationToken token) {
        if (caseSensitiveDirectory is not null) {
            var caseSensitive = default(bool);
            var worked = await caseSensitiveDirectory.Work(directory, value => caseSensitive = value, token);
            if (worked) {
                return caseSensitive;
            }
        }
        return await Task.Run(() => SevenZipArchive.IsDirectoryCaseSensitive(directory), token);
    }

    private static string FindExistingDirectory(string directory, CancellationToken token) {
        var current = Path.GetFullPath(directory);
        while (true) {
            token.ThrowIfCancellationRequested();
            if (Directory.Exists(current)) {
                return current;
            }
            var parent = Path.GetDirectoryName(current);
            var hasNoParent = string.IsNullOrEmpty(parent);
            var parentIsCurrent = string.Equals(parent, current, StringComparison.Ordinal);
            if (hasNoParent || parentIsCurrent) {
                return null;
            }
            current = parent;
        }
    }

    private static string ResolveStoredParentPath(string path, CancellationToken token = default) {
        token.ThrowIfCancellationRequested();
        var fullName = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullName);
        var fileName = Path.GetFileName(fullName);
        if (string.IsNullOrEmpty(directory)) {
            return fullName;
        }
        return Path.Combine(ResolveStoredPath(directory, token), fileName);
    }

    private static string ResolveStoredPath(string path, CancellationToken token = default) {
        token.ThrowIfCancellationRequested();
        var fullName = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullName);
        if (string.IsNullOrEmpty(root)) {
            return fullName;
        }
        root = NormalizeRoot(root);
        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        var components = fullName[root.Length..]
            .Split(separators, StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        foreach (var component in components) {
            token.ThrowIfCancellationRequested();
            var currentIsDirectory = Directory.Exists(current);
            if (currentIsDirectory is false) {
                current = Path.Combine(current, component);
                continue;
            }
            var currentIsCaseSensitive = SevenZipArchive.IsDirectoryCaseSensitive(current);
            try {
                var names = Directory.EnumerateFileSystemEntries(current)
                    .Select(Path.GetFileName)
                    .Where(name => string.Equals(name, component, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var exact = names.FirstOrDefault(name => string.Equals(name, component, StringComparison.Ordinal));
                var hasUniqueInsensitiveMatch = currentIsCaseSensitive is false && names.Length == 1;
                var selected = exact ?? (hasUniqueInsensitiveMatch ? names[0] : component);
                current = Path.Combine(current, selected);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception) {
                current = Path.Combine(current, component);
            }
        }
        return current;
    }

    private static string NormalizeRoot(string root) {
        if (Path.DirectorySeparatorChar != '\\') {
            return root;
        }
        if (root.StartsWith("\\\\", StringComparison.Ordinal)) {
            return root.ToUpperInvariant();
        }
        var rootHasDrivePrefix = root.Length >= 2 && root[1] == ':';
        if (rootHasDrivePrefix) {
            return char.ToUpperInvariant(root[0]) + root[1..];
        }
        return root;
    }

    internal string FullName { get; }

    internal bool IsCaseSensitive { get; }

    internal static async Task<SevenZipArchiveIdentity> Create(
        FileInfo file,
        ICaseCorrectFile caseCorrectFile,
        ICaseSensitiveDirectory caseSensitiveDirectory,
        CancellationToken token) {
        ArgumentNullException.ThrowIfNull(file);
        var fullName = Path.GetFullPath(file.FullName);
        if (caseCorrectFile is not null) {
            /*
             * File case correction requires an existing file. New archives use the stored
             * parent path below while preserving the requested archive name.
             */
            var exists = await Task.Run(() => File.Exists(fullName), token);
            if (exists) {
                var correctPath = default(string);
                var corrected = await caseCorrectFile.Work(fullName, path => correctPath = path, token);
                var correctedPathIsUsable = corrected && string.IsNullOrWhiteSpace(correctPath) is false;
                if (correctedPathIsUsable) {
                    fullName = Path.GetFullPath(correctPath);
                }
            }
        }
        fullName = await ResolveStoredFilePathAsync(fullName, caseSensitiveDirectory, token);
        var directory = Path.GetDirectoryName(fullName);
        var isCaseSensitive = await GetCaseSensitivityAsync(directory, caseSensitiveDirectory, token);
        token.ThrowIfCancellationRequested();
        return new SevenZipArchiveIdentity(fullName, isCaseSensitive);
    }

    internal static async Task<SevenZipArchiveIdentity> Create(FileInfo file, CancellationToken token) {
        var imports = Imports.Current;
        return await Create(
            file,
            imports.Find<ICaseCorrectFile>(throwIfNotFound: false, throwIfNotReady: false),
            imports.Find<ICaseSensitiveDirectory>(throwIfNotFound: false, throwIfNotReady: false),
            token);
    }

    internal static SevenZipArchiveIdentity CreateFallback(FileInfo file) {
        ArgumentNullException.ThrowIfNull(file);
        var fullName = Path.GetFullPath(file.FullName);
        fullName = ResolveStoredFilePath(fullName, CancellationToken.None);
        var directory = Path.GetDirectoryName(fullName);
        var isCaseSensitive = SevenZipArchive.IsDirectoryCaseSensitive(directory);
        return new SevenZipArchiveIdentity(fullName, isCaseSensitive);
    }

    public bool Equals(SevenZipArchiveIdentity other) {
        if (other is null) {
            return false;
        }
        return IsCaseSensitive == other.IsCaseSensitive && Comparer.Equals(FullName, other.FullName);
    }

    public override bool Equals(object obj) {
        return obj is SevenZipArchiveIdentity identity && Equals(identity);
    }

    public override int GetHashCode() {
        return HashCode.Combine(Kind, Comparer.GetHashCode(FullName));
    }
}
