using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Brows;

/// <summary>
/// Holds the temporary files that contain the messages extracted for viewing.
/// </summary>
/// <remarks>
/// A live owner holds an open handle to its marker file. Cleanup can therefore distinguish an
/// active session from a stale marker left behind after a crash.
/// </remarks>
internal static class MBoxTemp {
    private static readonly Lock Locker = new();
    private static readonly Dictionary<string, OwnerLease> Owners = new(StringComparer.OrdinalIgnoreCase);

    private sealed class OwnerLease(string directory, string sessionId, FileStream stream) {
        internal string Directory { get; } = directory;
        internal string SessionId { get; } = sessionId;
        internal FileStream Stream { get; } = stream;
    }

    /// <summary>
    /// Gets the number of days that an unused temporary directory is retained.
    /// </summary>
    public const int RetentionDays = 7;

    /// <summary>
    /// Gets the name of the file that marks a temporary directory as in use.
    /// </summary>
    public const string OwnerFileName = "owner.txt";

    /// <summary>
    /// Gets the root directory that holds the temporary directories of the feature.
    /// </summary>
    public static string Root {
        get {
            return Path.Combine(
                Path.GetTempPath(),
                "Brows",
                MBoxPath.DirectoryName);
        }
    }

    /// <summary>
    /// Returns the directory that holds the temporary files of the specified session, and creates
    /// it when it does not exist.
    /// </summary>
    /// <param name="sessionId">
    /// The identifier of the session.
    /// </param>
    /// <returns>
    /// The directory that holds the temporary files of the session.
    /// </returns>
    public static string SessionDirectory(string sessionId) {
        return SessionDirectory(sessionId, Root);
    }

    internal static string SessionDirectory(string sessionId, string root) {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var directory = MBoxPath.SessionDirectory(sessionId, root);
        if (IsUnder(root, directory) == false) {
            throw new InvalidOperationException("The temporary directory is outside the root of the feature.");
        }
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static void Acquire(string sessionId, string root = null) {
        var directory = SessionDirectory(sessionId, root ?? Root);
        var owner = Path.Combine(directory, OwnerFileName);
        lock (Locker) {
            if (Owners.ContainsKey(directory)) {
                return;
            }
            var stream = new FileStream(owner, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
            try {
                var bytes = Encoding.UTF8.GetBytes(sessionId);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
                Owners.Add(directory, new OwnerLease(directory, sessionId, stream));
            }
            catch {
                stream.Dispose();
                throw;
            }
        }
    }

    /// <summary>
    /// Returns whether the specified path resolves to a location that is under the specified root.
    /// </summary>
    /// <param name="root">
    /// The root directory.
    /// </param>
    /// <param name="path">
    /// The path to check.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the specified path is under the specified root.
    /// </returns>
    public static bool IsUnder(string root, string path) {
        var rootIsMissing = string.IsNullOrWhiteSpace(root);
        var pathIsMissing = string.IsNullOrWhiteSpace(path);
        var rootOrPathIsMissing = rootIsMissing || pathIsMissing;
        if (rootOrPathIsMissing) {
            return false;
        }
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(path);
        var rootWithSeparator = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Removes temporary directories that are older than the retention period and that no live
    /// session owns.
    /// </summary>
    public static void Clean() {
        Clean(Root, DateTime.UtcNow);
    }

    internal static void Clean(string root, DateTime utcNow) {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        if (Directory.Exists(root) == false) {
            return;
        }
        try {
            if ((new DirectoryInfo(root).Attributes & FileAttributes.ReparsePoint) != 0) {
                return;
            }
            var cutoff = utcNow.ToUniversalTime().AddDays(-RetentionDays);
            foreach (var directory in SafeDirectories(root)) {
                try {
                    if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) {
                        continue;
                    }
                    if (IsOwned(directory.FullName)) {
                        continue;
                    }
                    if (directory.LastWriteTimeUtc > cutoff) {
                        continue;
                    }
                    TryRemove(directory.FullName);
                }
                catch (Exception) {
                    /*
                     * An unreadable temporary directory is left for a later cleanup attempt.
                     */
                }
            }
        }
        catch (Exception) {
            /*
             * Cleanup must not fail feature initialization when its root cannot be inspected.
             */
        }
    }

    internal static void Shutdown(string root) {
        var fullRoot = Path.GetFullPath(root);
        var release = new List<OwnerLease>();
        lock (Locker) {
            foreach (var path in Owners.Keys.ToArray()) {
                if (IsUnder(fullRoot, path)) {
                    release.Add(Owners[path]);
                    Owners.Remove(path);
                }
            }
        }
        foreach (var lease in release) {
            Release(lease, fullRoot);
        }
    }

    public static void Shutdown() {
        var release = new List<OwnerLease>();
        lock (Locker) {
            release.AddRange(Owners.Values);
            Owners.Clear();
        }
        foreach (var lease in release) {
            var root = Path.GetDirectoryName(lease.Directory);
            if (string.IsNullOrEmpty(root) == false) {
                Release(lease, root);
            }
        }
    }

    private static void Release(OwnerLease lease, string root) {
        try {
            lease.Stream.Dispose();
        }
        catch (Exception) {
        }
        TryRemoveOwnedDirectory(root, lease.Directory, lease.SessionId);
    }

    private static void TryRemoveOwnedDirectory(string root, string directory, string sessionId) {
        try {
            var pathIsUnderRoot = IsUnder(root, directory);
            var directoryExists = Directory.Exists(directory);
            var directoryIsInvalid = pathIsUnderRoot == false || directoryExists == false;
            if (directoryIsInvalid) {
                return;
            }
            var info = new DirectoryInfo(directory);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0) {
                return;
            }
            var owner = Path.Combine(directory, OwnerFileName);
            var ownerExists = File.Exists(owner);
            var ownerIsNotOurs = ownerExists == false || File.ReadAllText(owner, Encoding.UTF8) != sessionId;
            if (ownerIsNotOurs) {
                return;
            }
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception) {
            /*
             * A viewer may still have a message open. Cleanup will retry after the retention period.
             */
        }
    }

    private static void TryRemove(string directory) {
        try {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException) {
            /*
             * A file that is still open stays until a later cleanup.
             */
        }
        catch (UnauthorizedAccessException) {
        }
    }

    private static bool IsOwned(string directory) {
        var owner = Path.Combine(directory, OwnerFileName);
        try {
            using var stream = new FileStream(owner, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (FileNotFoundException) {
            return false;
        }
        catch (IOException) {
            return true;
        }
        catch (UnauthorizedAccessException) {
            return true;
        }
    }

    private static DirectoryInfo[] SafeDirectories(string root) {
        try {
            return new DirectoryInfo(root).GetDirectories();
        }
        catch (Exception) {
            return [];
        }
    }
}
