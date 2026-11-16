using Brows.Entries;
using Brows.Operations;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Brows.Composition;

internal sealed class ClipboardSetIO : IClipboardSetIO {
    private const string StagingDirectoryName = "BrowsClipboard";
    private static readonly ILog Log = Logging.For(typeof(ClipboardSetIO));
    private readonly IClipboardTransport Clipboard;

    private static string CreateStagingRoot() {
        var directory = Path.Combine(Path.GetTempPath(), StagingDirectoryName, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string ResolveStagedPath(string root, string relativePath) {
        if (string.IsNullOrWhiteSpace(relativePath)) {
            throw new InvalidDataException("A clipboard stream source must have a relative path.");
        }
        var normalizedPath = relativePath
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Replace(Path.DirectorySeparatorChar == '\\' ? '/' : '\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalizedPath)) {
            throw new InvalidDataException("A clipboard stream source path must be relative.");
        }
        foreach (var component in normalizedPath.Split(Path.DirectorySeparatorChar)) {
            var aliasesWindowsPath = component.EndsWith(' ') ||
                (component.EndsWith('.') && component is not "." and not "..");
            if (aliasesWindowsPath) {
                throw new InvalidDataException("A clipboard stream source contains a Windows path alias.");
            }
        }
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, normalizedPath));
        var relativeToRoot = Path.GetRelativePath(fullRoot, fullPath);
        var escapesRoot = relativeToRoot == ".." ||
            relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relativeToRoot);
        if (escapesRoot) {
            throw new InvalidDataException("A clipboard stream source path cannot escape its staging directory.");
        }
        return fullPath;
    }

    private static void ValidateStagedPathSpelling(
        string root,
        string path,
        Dictionary<string, string> spellings) {
        var relativePath = Path.GetRelativePath(root, path);
        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        var components = relativePath.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        var current = string.Empty;
        foreach (var component in components) {
            current = string.IsNullOrEmpty(current)
                ? component
                : Path.Combine(current, component);
            if (spellings.TryGetValue(current, out var existingSpelling)) {
                var spellingDiffers = string.Equals(existingSpelling, current, StringComparison.Ordinal) == false;
                if (spellingDiffers) {
                    throw new InvalidDataException(
                        $"Clipboard stream paths '{existingSpelling}' and '{current}' differ only by case.");
                }
            }
            else {
                spellings.Add(current, current);
            }
        }
    }

    private static void AddUniqueEntryPath(
        string path,
        HashSet<string> entryPaths,
        List<string> dropPaths,
        HashSet<string> dropPathSet) {
        if (entryPaths.Add(path)) {
            AddDropPath(path, dropPaths, dropPathSet);
        }
    }

    private static void AddDropPath(string path, List<string> dropPaths, HashSet<string> dropPathSet) {
        if (dropPathSet.Add(path)) {
            dropPaths.Add(path);
        }
    }

    private static List<string> WithoutNestedEntries(IReadOnlyList<string> paths) {
        var directories = paths
            .Where(Directory.Exists)
            .Select(Path.GetFullPath)
            .ToArray();
        var result = new List<string>();
        foreach (var path in paths) {
            var isNested = directories.Any(directory => {
                var isDifferentDirectory = string.Equals(path, directory, StringComparison.Ordinal) == false;
                var isWithinDirectory = IsInside(path, directory);
                return isDifferentDirectory && isWithinDirectory;
            });
            if (isNested == false) {
                result.Add(path);
            }
        }
        return result;
    }

    private static bool IsInside(string path, string directory) {
        var fullPath = Path.GetFullPath(path);
        var fullDirectory = Path.GetFullPath(directory);
        var directoryPrefix = Path.EndsInDirectorySeparator(fullDirectory)
            ? fullDirectory
            : fullDirectory + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(directoryPrefix, StringComparison.Ordinal);
    }

    private static void TryDeleteStagingRoot(string stagingRoot) {
        if (stagingRoot is null) {
            return;
        }
        try {
            Directory.Delete(stagingRoot, recursive: true);
        }
        catch (Exception ex) {
            if (Log.Error()) {
                Log.Error(ex);
            }
        }
    }

    internal ClipboardSetIO(IClipboardTransport clipboard) {
        Clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
    }

    public ClipboardSetIO() : this(new WindowsClipboardTransport()) {
    }

    public async Task<bool> Work(
        IEnumerable<IProvidedIO> collection,
        IClipboardSetIOData data,
        IOperationProgress progress,
        CancellationToken token) {
        if (collection is null) {
            return false;
        }
        var dropPaths = new List<string>();
        var dropPathSet = new HashSet<string>(StringComparer.Ordinal);

        string stagingRoot = null;
        var ownershipTransferred = false;
        try {
            var providedItems = collection.ToArray();
            if (providedItems.Length == 0) {
                return false;
            }
            var streamSetIndex = 0;
            foreach (var providedItem in providedItems) {
                token.ThrowIfCancellationRequested();
                var streamSets = providedItem?.StreamSets?
                    .Where(streamSet => streamSet is not null)
                    .ToArray() ?? Array.Empty<IEntryStreamSet>();
                foreach (var streamSet in streamSets) {
                    token.ThrowIfCancellationRequested();
                    var currentStreamSetIndex = streamSetIndex++;
                    var directFiles = streamSet.FileSource()?
                        .Where(file => string.IsNullOrWhiteSpace(file) == false)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray() ?? Array.Empty<string>();
                    if (directFiles.Length > 0) {
                        foreach (var file in directFiles) {
                            AddDropPath(Path.GetFullPath(file), dropPaths, dropPathSet);
                        }
                        continue;
                    }
                    var streamRoot = default(string);
                    var entryPaths = new HashSet<string>(StringComparer.Ordinal);
                    var stagedPathSpellings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    using (streamSet.StreamSourceReady()) {
                        await foreach (var source in streamSet.StreamSource(token).WithCancellation(token)) {
                            token.ThrowIfCancellationRequested();
                            if (source is null) {
                                continue;
                            }
                            var sourceFile = source.SourceFile;
                            var sourceFileExists = string.IsNullOrWhiteSpace(sourceFile) == false &&
                                (File.Exists(sourceFile) || Directory.Exists(sourceFile));
                            if (sourceFileExists) {
                                AddDropPath(Path.GetFullPath(sourceFile), dropPaths, dropPathSet);
                                continue;
                            }
                            var sourceDirectory = source.SourceDirectory;
                            if (streamRoot is null) {
                                stagingRoot ??= CreateStagingRoot();
                                streamRoot = Path.Combine(stagingRoot, $"set-{currentStreamSetIndex:D4}");
                                Directory.CreateDirectory(streamRoot);
                            }
                            if (string.IsNullOrWhiteSpace(sourceDirectory) == false) {
                                var directoryPath = ResolveStagedPath(streamRoot, source.RelativePath);
                                ValidateStagedPathSpelling(streamRoot, directoryPath, stagedPathSpellings);
                                Directory.CreateDirectory(directoryPath);
                                AddUniqueEntryPath(directoryPath, entryPaths, dropPaths, dropPathSet);
                                continue;
                            }
                            var targetPath = ResolveStagedPath(streamRoot, source.RelativePath);
                            ValidateStagedPathSpelling(streamRoot, targetPath, stagedPathSpellings);
                            if (entryPaths.Add(targetPath) == false) {
                                continue;
                            }
                            var targetDirectory = Path.GetDirectoryName(targetPath);
                            if (string.IsNullOrEmpty(targetDirectory) == false) {
                                Directory.CreateDirectory(targetDirectory);
                            }
                            using var ready = await source.StreamReady(token);
                            await using var sourceStream = source.Stream;
                            if (sourceStream is null) {
                                throw new IOException($"The clipboard source '{source.EntryName}' has no stream.");
                            }
                            await using var targetStream = new FileStream(
                                targetPath,
                                FileMode.CreateNew,
                                FileAccess.Write,
                                FileShare.None,
                                bufferSize: 81920,
                                useAsync: true);
                            await sourceStream.CopyToAsync(targetStream, token);
                            AddDropPath(targetPath, dropPaths, dropPathSet);
                        }
                    }
                }
            }
            var clipboardPaths = WithoutNestedEntries(dropPaths);
            if (clipboardPaths.Count == 0) {
                TryDeleteStagingRoot(stagingRoot);
                return false;
            }
            var dropList = new StringCollection();
            dropList.AddRange(clipboardPaths.ToArray());
            var effect = data?.MoveOnPaste == true
                ? DragDropEffects.Move
                : DragDropEffects.Copy;
            token.ThrowIfCancellationRequested();
            /*
             * The file-drop list contains paths, so consumers may open staged files after this process exits.
             * Retain successful staging directories; only failed preparations are removed.
             */
            ownershipTransferred = stagingRoot is not null;
            Clipboard.PublishFileDropList(dropList, effect);
            return true;
        }
        catch (OperationCanceledException ex) {
            if (Log.Debug()) {
                Log.Debug(ex);
            }
            if (ownershipTransferred == false) {
                TryDeleteStagingRoot(stagingRoot);
            }
            throw;
        }
        catch (Exception ex) {
            if (Log.Error()) {
                Log.Error(ex);
            }
            if (ownershipTransferred == false) {
                TryDeleteStagingRoot(stagingRoot);
            }
            return false;
        }
    }
}
