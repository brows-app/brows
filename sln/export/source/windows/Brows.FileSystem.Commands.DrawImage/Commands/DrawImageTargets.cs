using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

/*
 * Decides where each image of a batch is published. Names are planned once,
 * in the sorted order of the selection, before any image is converted:
 *   - The first source to ask for a path may use it, unless the path is the
 *     source of another image in the batch (also through an alias of its
 *     directory), or a directory, or an existing file while overwrite is off.
 *   - Every other source is given the next free suffixed name for its path.
 * Sources are told apart by their exact paths, because a case-sensitive
 * directory can hold names that differ only by case. Output names are
 * compared without regard to case, and by the directory they resolve to
 * through junctions and symbolic links, so two requests never replace the
 * same file. A planned name is claimed by moving the encoded file into place
 * without replacing anything, and an existing file is overwritten with
 * File.Replace, which keeps its streams and attributes.
 */
internal sealed class DrawImageTargets {
    private const int LinkLimit = 64;

    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;
    private static readonly StringComparer SourceComparer = StringComparer.Ordinal;

    private readonly Lock Locker = new();
    private readonly Dictionary<string, string> Requested = new(SourceComparer);
    private readonly Dictionary<string, string> Planned = new(SourceComparer);
    private readonly HashSet<string> Exact = new(SourceComparer);
    private readonly Dictionary<string, int> NextSuffix = new(Comparer);
    private readonly HashSet<string> Reserved = new(Comparer);
    private readonly IReadOnlyDictionary<string, string> Directories;

    private DrawImageTargets(IReadOnlyList<KeyValuePair<FileInfo, string>> requests, bool overwrite,
                             IReadOnlyDictionary<string, string> directories, IReadOnlySet<string> ownPaths,
                             CancellationToken token) {
        Overwrite = overwrite;
        Directories = directories;
        var sources = new HashSet<string>(Comparer);
        foreach (var request in requests) {
            token.ThrowIfCancellationRequested();
            sources.Add(Canonical(request.Key.FullName));
        }
        foreach (var request in requests) {
            token.ThrowIfCancellationRequested();
            var source = request.Key.FullName;
            var path = request.Value;
            Requested[source] = path;
            var canonical = Canonical(path);
            var pathIsOwnSource = SourceComparer.Equals(path, source)
                || SourceComparer.Equals(canonical, Canonical(source))
                || ownPaths.Contains(source);
            var pathIsOtherSource = pathIsOwnSource == false && sources.Contains(canonical);
            var pathIsBlocked = pathIsOtherSource
                || Directory.Exists(path)
                || (overwrite == false && File.Exists(path));
            if (pathIsBlocked) {
                continue;
            }
            if (Reserved.Add(canonical)) {
                Exact.Add(source);
            }
        }
        foreach (var request in requests) {
            token.ThrowIfCancellationRequested();
            var source = request.Key.FullName;
            if (Exact.Contains(source) == false) {
                Planned[source] = NextFree(request.Value, token);
            }
        }
    }

    private static string LinkTarget(string path) {
        try {
            var info = new DirectoryInfo(path);
            if (info.Exists == false || info.LinkTarget is null) {
                return null;
            }
            return info.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
        }
        catch (IOException) {
            return null;
        }
        catch (UnauthorizedAccessException) {
            return null;
        }
    }

    /*
     * Follows every junction and symbolic link in the directory's path. Aliases
     * that are not links, such as substituted drives, are found separately with
     * the path equivalence check in Plan.
     */
    private static string Resolve(string directory) {
        var path = Path.GetFullPath(directory);
        for (var links = 0; links < LinkLimit; links++) {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root)) {
                return path;
            }
            var parts = path
                .Substring(root.Length)
                .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            var target = default(string);
            var index = 0;
            for (; index < parts.Length; index++) {
                current = Path.Combine(current, parts[index]);
                target = LinkTarget(current);
                if (target is not null) {
                    break;
                }
            }
            if (target is null) {
                return current;
            }
            path = Path.Combine([target, .. parts.Skip(index + 1)]);
        }
        return path;
    }

    private static string Canonical(string path, IReadOnlyDictionary<string, string> directories) {
        var directory = Path.GetDirectoryName(path);
        if (directory is not null && directories.TryGetValue(directory, out var actual)) {
            return Path.Combine(actual, Path.GetFileName(path));
        }
        return path;
    }

    /*
     * Removes the extended-length or device prefix from a drive or UNC path,
     * because GetFullPath leaves such a path as written. Without the prefix,
     * GetFullPath normalizes the path and expands an 8.3 short name of an
     * existing file, so another source cannot be reached through its short
     * name or a different spelling of its path. The prefix is kept when
     * normalizing would name a different file, as it would for a "." or ".."
     * segment or a name ending in a period or space, which Windows takes
     * literally only in a prefixed path.
     */
    private static string FullPath(string path) {
        var prefixed = path.StartsWith(@"\\?\", StringComparison.Ordinal)
            || path.StartsWith(@"\\.\", StringComparison.Ordinal);
        if (prefixed) {
            var rest = path.Substring(4);
            var restIsShare = rest.StartsWith(@"UNC\", StringComparison.OrdinalIgnoreCase);
            var restIsDrive = rest.Length >= 3
                && char.IsLetter(rest[0])
                && rest[1] == Path.VolumeSeparatorChar
                && rest[2] == Path.DirectorySeparatorChar;
            if (restIsShare && NormalizingKeepsNames(rest.Substring(4))) {
                path = @"\\" + rest.Substring(4);
            }
            if (restIsDrive && NormalizingKeepsNames(rest.Substring(3))) {
                path = rest;
            }
        }
        return Path.GetFullPath(path);
    }

    private static bool NormalizingKeepsNames(string relative) {
        if (relative.Contains(Path.AltDirectorySeparatorChar)) {
            return false;
        }
        foreach (var name in relative.Split(Path.DirectorySeparatorChar)) {
            var nameChanges = name.Length == 0
                || name == "."
                || name == ".."
                || name.EndsWith('.')
                || name.EndsWith(' ');
            if (nameChanges) {
                return false;
            }
        }
        return true;
    }

    private static string Suffixed(string path, int number) {
        var directory = Path.GetDirectoryName(path);
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        return Path.Combine(directory, $"{name} ({number}){extension}");
    }

    private static bool TryMove(string temporary, string path) {
        try {
            File.Move(temporary, path, overwrite: false);
            return true;
        }
        catch (IOException) when (File.Exists(path) || Directory.Exists(path)) {
            return false;
        }
    }

    private static bool TryReplace(string temporary, string path) {
        if (Directory.Exists(path)) {
            return false;
        }
        if (File.Exists(path)) {
            try {
                File.Replace(temporary, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
                return true;
            }
            catch (FileNotFoundException) when (File.Exists(temporary) && File.Exists(path) == false) {
            }
        }
        File.Move(temporary, path, overwrite: true);
        return true;
    }

    private string Canonical(string path) {
        return Canonical(path, Directories);
    }

    /*
     * Each requested path remembers its next suffix, so suffixes are searched
     * once per batch instead of again from 1 for every source.
     */
    private string NextFree(string path, CancellationToken token) {
        var key = Canonical(path);
        var number = NextSuffix.GetValueOrDefault(key, 1);
        for (; number < int.MaxValue; number++) {
            token.ThrowIfCancellationRequested();
            var candidate = Suffixed(path, number);
            var canonical = Canonical(candidate);
            var candidateIsTaken = Reserved.Contains(canonical)
                || File.Exists(candidate)
                || Directory.Exists(candidate);
            if (candidateIsTaken) {
                continue;
            }
            Reserved.Add(canonical);
            NextSuffix[key] = number + 1;
            return candidate;
        }
        throw new IOException($"No available output name for [{path}].");
    }

    private string RequestedPath(FileInfo source) {
        ArgumentNullException.ThrowIfNull(source);
        if (Requested.TryGetValue(source.FullName, out var path)) {
            return path;
        }
        throw new ArgumentException($"No output was requested for [{source.FullName}].", nameof(source));
    }

    internal bool Overwrite { get; }

    /*
     * Resolves each source and destination directory once. An existing
     * destination directory that is still not known as a source directory is
     * also compared with each source directory, which finds aliases such as
     * substituted drives, so the planner can tell when an existing
     * destination file is another source.
     */
    internal static async Task<DrawImageTargets> Plan(
            IEnumerable<KeyValuePair<FileInfo, string>> requests,
            bool overwrite,
            Func<string, string, CancellationToken, Task<bool>> pathsAreEquivalent,
            CancellationToken token) {
        ArgumentNullException.ThrowIfNull(requests);
        token.ThrowIfCancellationRequested();
        var list = new List<KeyValuePair<FileInfo, string>>();
        foreach (var request in requests) {
            token.ThrowIfCancellationRequested();
            list.Add(new(request.Key, FullPath(request.Value)));
        }
        var sourceDirectories = list
            .Select(request => request.Key.DirectoryName)
            .Distinct(Comparer)
            .ToList();
        var requestDirectories = list
            .Select(request => Path.GetDirectoryName(request.Value))
            .Where(directory => directory is not null)
            .Distinct(Comparer)
            .ToList();
        var directories = new Dictionary<string, string>(Comparer);
        foreach (var directory in sourceDirectories.Concat(requestDirectories)) {
            token.ThrowIfCancellationRequested();
            if (directories.ContainsKey(directory) == false) {
                directories[directory] = Resolve(directory);
            }
        }
        if (overwrite && pathsAreEquivalent is not null) {
            var resolvedSources = new HashSet<string>(sourceDirectories.Select(directory => directories[directory]),
                                                      Comparer);
            var destinationDirectories = list
                .Where(request => File.Exists(request.Value))
                .Select(request => Path.GetDirectoryName(request.Value))
                .Where(directory => directory is not null && resolvedSources.Contains(directories[directory]) == false)
                .Distinct(Comparer)
                .ToList();
            foreach (var destination in destinationDirectories) {
                foreach (var source in sourceDirectories) {
                    if (await pathsAreEquivalent(destination, source, token)) {
                        directories[destination] = directories[source];
                        break;
                    }
                }
            }
        }
        /*
         * A requested path that differs from its source only by case, after
         * both directories are resolved, is the source itself in a
         * case-insensitive directory, but another file in a case-sensitive
         * one, so the file system decides.
         */
        var ownPaths = new HashSet<string>(SourceComparer);
        if (overwrite && pathsAreEquivalent is not null) {
            foreach (var request in list) {
                token.ThrowIfCancellationRequested();
                var source = request.Key.FullName;
                var path = request.Value;
                var canonicalSource = Canonical(source, directories);
                var canonicalPath = Canonical(path, directories);
                var pathDiffersOnlyByCase = Comparer.Equals(canonicalPath, canonicalSource)
                    && SourceComparer.Equals(canonicalPath, canonicalSource) == false;
                if (pathDiffersOnlyByCase && File.Exists(path) && await pathsAreEquivalent(path, source, token)) {
                    ownPaths.Add(source);
                }
            }
        }
        return new DrawImageTargets(list, overwrite, directories, ownPaths, token);
    }

    internal string Output(FileInfo source) {
        return RequestedPath(source);
    }

    internal string Temporary(FileInfo source) {
        var path = RequestedPath(source);
        var directory = Path.GetDirectoryName(path);
        var extension = Path.GetExtension(path);
        return Path.Combine(directory, $"~{Guid.NewGuid():N}{extension}");
    }

    internal string Publish(FileInfo source, string temporary) {
        ArgumentNullException.ThrowIfNull(temporary);
        var path = RequestedPath(source);
        lock (Locker) {
            if (Exact.Contains(source.FullName)) {
                var published = Overwrite
                    ? TryReplace(temporary, path)
                    : TryMove(temporary, path);
                if (published) {
                    return path;
                }
            }
            else {
                var planned = Planned[source.FullName];
                if (TryMove(temporary, planned)) {
                    return planned;
                }
            }
            while (true) {
                var candidate = NextFree(path, CancellationToken.None);
                if (TryMove(temporary, candidate)) {
                    return candidate;
                }
            }
        }
    }
}
