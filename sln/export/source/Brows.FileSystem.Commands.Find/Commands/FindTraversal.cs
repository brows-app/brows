using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Brows.Commands;

internal static class FindTraversal {
    private static EnumerationOptions CreateOptions(bool ignoreInaccessible) {
        return new EnumerationOptions {
            AttributesToSkip = 0,
            IgnoreInaccessible = ignoreInaccessible,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false
        };
    }

    private static bool IsRecoverable(Exception exception) {
        return exception is DirectoryNotFoundException or FileNotFoundException or UnauthorizedAccessException;
    }

    private static bool TryGetAttributes(FileSystemInfo info, Action onError, out FileAttributes attributes) {
        try {
            attributes = info.Attributes;
            return true;
        }
        catch (Exception exception) when (IsRecoverable(exception)) {
            onError?.Invoke();
            attributes = default;
            return false;
        }
    }

    private static bool IsExcluded(
        FileSystemInfo info,
        IReadOnlyList<IMatcher> exclusions,
        Action onError,
        CancellationToken token) {
        for (var i = 0; i < exclusions.Count; i++) {
            token.ThrowIfCancellationRequested();
            try {
                if (exclusions[i].Matches(info.FullName)) {
                    return true;
                }
            }
            catch (RegexMatchTimeoutException) {
                onError?.Invoke();
                return true;
            }
        }
        return false;
    }

    private static IEnumerator<FileSystemInfo> OpenDirectory(DirectoryInfo directory, EnumerationOptions options) {
        return directory.EnumerateFileSystemInfos("*", options).GetEnumerator();
    }

    private static IEnumerator<FileSystemInfo> OpenChildDirectory(
        DirectoryInfo directory,
        EnumerationOptions options,
        Action onError) {
        try {
            return OpenDirectory(directory, options);
        }
        catch (Exception exception) when (IsRecoverable(exception)) {
            onError?.Invoke();
            return null;
        }
    }

    private static void DisposeEnumerator(IEnumerator<FileSystemInfo> enumerator, Action onError) {
        try {
            enumerator.Dispose();
        }
        catch (Exception exception) when (IsRecoverable(exception)) {
            onError?.Invoke();
        }
    }

    private static async IAsyncEnumerable<FileSystemInfo> EnumerateCore(
        DirectoryInfo root,
        bool recurse,
        IReadOnlyList<IMatcher> exclusions,
        Action onError,
        [EnumeratorCancellation] CancellationToken token) {
        await Task.CompletedTask.ConfigureAwait(false);
        token.ThrowIfCancellationRequested();

        var rootOptions = CreateOptions(ignoreInaccessible: false);
        var childOptions = CreateOptions(ignoreInaccessible: false);
        var stack = new Stack<(IEnumerator<FileSystemInfo> Enumerator, bool IsRoot)>();
        var rootEnumerator = OpenDirectory(root, rootOptions);
        stack.Push((rootEnumerator, true));

        try {
            while (stack.Count > 0) {
                token.ThrowIfCancellationRequested();
                var frame = stack.Peek();
                FileSystemInfo item = null;
                var hasItem = false;
                try {
                    hasItem = frame.Enumerator.MoveNext();
                    if (hasItem) {
                        item = frame.Enumerator.Current;
                    }
                }
                catch (Exception exception) when (!frame.IsRoot && IsRecoverable(exception)) {
                    onError?.Invoke();
                    DisposeEnumerator(stack.Pop().Enumerator, onError);
                    continue;
                }

                if (!hasItem) {
                    DisposeEnumerator(stack.Pop().Enumerator, onError);
                    continue;
                }
                var itemIsMissingOrExcluded = item is null || IsExcluded(item, exclusions, onError, token);
                if (itemIsMissingOrExcluded) {
                    continue;
                }

                var hasAttributes = TryGetAttributes(item, onError, out var attributes);
                var itemIsDirectory = hasAttributes && (attributes & FileAttributes.Directory) != 0;
                var itemIsReparsePoint = itemIsDirectory && (attributes & FileAttributes.ReparsePoint) != 0;
                var descend = recurse && itemIsDirectory && !itemIsReparsePoint;
                yield return item;

                if (descend) {
                    token.ThrowIfCancellationRequested();
                    var childEnumerator = OpenChildDirectory((DirectoryInfo)item, childOptions, onError);
                    if (childEnumerator is not null) {
                        stack.Push((childEnumerator, false));
                    }
                }
            }
        }
        finally {
            while (stack.Count > 0) {
                DisposeEnumerator(stack.Pop().Enumerator, onError);
            }
        }
    }

    internal static IAsyncEnumerable<FileSystemInfo> Enumerate(
        DirectoryInfo root,
        bool recurse,
        IReadOnlyList<IMatcher> exclusions,
        CancellationToken token) {
        return Enumerate(root, recurse, exclusions, null, token);
    }

    internal static IAsyncEnumerable<FileSystemInfo> Enumerate(
        DirectoryInfo root,
        bool recurse,
        IReadOnlyList<IMatcher> exclusions,
        Action onError,
        CancellationToken token) {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(exclusions);
        return EnumerateCore(root, recurse, exclusions, onError, token);
    }
}
