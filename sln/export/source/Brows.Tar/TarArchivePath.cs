using Brows.Shared.Archives;
using Domore.IO;
using Domore.Logs;
using Domore.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TarArchiveEntry = System.Formats.Tar.TarEntry;

namespace Brows;

internal sealed class TarArchivePath {
    private static readonly ILog Log = Logging.For(typeof(TarArchivePath));
    private static readonly Lock SetLocker = new();
    private static readonly Dictionary<(TarArchiveIdentity Identity, NestKey Nest), TarArchivePath> Set =
        [];

    private sealed class NestKey : IEquatable<NestKey> {
        private readonly string[] Members;

        public NestKey(IEnumerable<string> members) {
            Members = members?.ToArray() ?? throw new ArgumentNullException(nameof(members));
        }

        public bool Equals(NestKey other) {
            if (other is null || Members.Length != other.Members.Length) {
                return false;
            }

            for (var index = 0; index < Members.Length; index++) {
                var membersAreEqual = string.Equals(Members[index], other.Members[index], StringComparison.Ordinal);
                if (membersAreEqual is false) {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object other) {
            return other is NestKey key && Equals(key);
        }

        public override int GetHashCode() {
            var hashCode = new HashCode();
            foreach (var member in Members) {
                hashCode.Add(member, StringComparer.Ordinal);
            }
            return hashCode.ToHashCode();
        }
    }

    private static string MakeFullName(TarArchiveIdentity identity, TarArchiveNest nest) {
        return ArchiveProviderIDCodec.Format(new ArchiveProviderID(
            ArchiveProviderIDFormat.Tar,
            identity.FullName,
            nest,
            ""));
    }

    /*
     * An archive may hold metadata records that do not name items of the
     * archive. The reader resolves the metadata of the items that follow
     * them, and the adapter skips them here.
     */
    private static TarEntryKind? EntryKindOf(TarArchiveEntry entry) {
        switch (entry.EntryType) {
            case TarEntryType.Directory:
                return TarEntryKind.Path;
            case TarEntryType.GlobalExtendedAttributes:
            case TarEntryType.LongLink:
            case TarEntryType.LongPath:
            case TarEntryType.ExtendedAttributes:
            case TarEntryType.DirectoryList:
            case TarEntryType.MultiVolume:
            case TarEntryType.TapeVolume:
            case TarEntryType.RenamedOrSymlinked:
                return null;
            default:
                return TarEntryKind.File;
        }
    }

    private static bool IsEmptyDataEntry(TarArchiveEntry entry) {
        var isDataEntryType = entry.EntryType is (
            TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.ContiguousFile);
        return entry.Length == 0 && isDataEntryType;
    }

    private static FileStream ArchiveStream(
        string fileName,
        FileMode mode = FileMode.Open,
        FileAccess access = FileAccess.Read) {
        var share = mode == FileMode.Open
            ? FileShare.Read
            : FileShare.None;
        return new FileStream(
            fileName,
            mode,
            access,
            share,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    private static async Task RunArchiveOperation(string name,
                                                  IOperationProgress progress,
                                                  Func<IOperationProgress, CancellationToken, Task> work,
                                                  CancellationToken token) {
        ArgumentNullException.ThrowIfNull(work);
        if (progress is null) {
            await Task.Run(() => work(null, token), token);
            return;
        }
        await progress.Child(name, async (childProgress, childToken) => {
            childProgress.Change(kind: OperationProgressKind.FileSize);
            await Task.Run(() => work(childProgress, childToken), childToken);
        }, OperationFailureBehavior.Throw, token);
    }

    private static async Task CopyStream(Stream source,
                                         Stream destination,
                                         long length,
                                         IOperationProgress progress,
                                         CancellationToken token) {
        var lengthIsKnown = length >= 0;
        progress?.Change(
            addTarget: Math.Max(0, length),
            kind: OperationProgressKind.FileSize);
        var buffer = new byte[81920];
        for (;;) {
            token.ThrowIfCancellationRequested();
            var read = await source.ReadAsync(buffer.AsMemory(), token);
            if (read == 0) {
                break;
            }
            token.ThrowIfCancellationRequested();
            await destination.WriteAsync(buffer.AsMemory(0, read), token);
            progress?.Change(addProgress: read, addTarget: lengthIsKnown ? null : read);
        }
    }

    private static string NormalizeArchiveSeparators(string path) {
        return path.Replace('\\', '/');
    }

    private static string TrimNameSeparators(string name) {
        return name.TrimEnd('/', '\\');
    }

    private static string NormalizeArchiveName(string name) {
        return TrimNameSeparators(NormalizeArchiveSeparators(new TarEntryName(name).Normalized));
    }

    private static string NormalizeArchiveRelativePath(string path) {
        return NormalizeArchiveRelativePath(path, allowEmpty: false);
    }

    private static string NormalizeArchiveEntryRelativePath(string path) {
        try {
            return NormalizeArchiveRelativePath(path, allowEmpty: true);
        }
        catch (ArgumentException ex) {
            throw new InvalidDataException("The archive entry name is not a safe relative path.", ex);
        }
    }

    private static string NormalizeArchiveRelativePath(string path, bool allowEmpty) {
        ArgumentNullException.ThrowIfNull(path);
        var originalStartsWithSeparator = path.Length > 0 && path[0] is '/' or '\\';
        var nameForChecks = path.Trim();
        var nameStartsWithSeparator = nameForChecks.Length > 0 && nameForChecks[0] is '/' or '\\';
        var hasDrivePrefix = nameForChecks.Length >= 2 && char.IsLetter(nameForChecks[0]) && nameForChecks[1] == ':';
        var hasParentComponent = nameForChecks
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Any(part => part.Trim() == "..");
        var isUnsafe = originalStartsWithSeparator ||
                       nameStartsWithSeparator ||
                       Path.IsPathRooted(nameForChecks) ||
                       hasDrivePrefix ||
                       hasParentComponent;
        if (isUnsafe) {
            throw new ArgumentException(
                "The archive name must be a relative path without parent components.",
                nameof(path));
        }
        var pathParts = path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        var normalized = string.Join('/', pathParts.Where(part => part != "."));
        var isRootMarker = pathParts.Length > 0 && normalized.Length == 0;
        var normalizedHasDrivePrefix = normalized.Length >= 2 &&
                                       char.IsLetter(normalized[0]) && normalized[1] == ':';
        if (normalizedHasDrivePrefix) {
            throw new ArgumentException(
                "The archive name must be a relative path without parent components.",
                nameof(path));
        }
        var normalizedIsInvalidlyEmpty = allowEmpty
            ? normalized.Length == 0 && isRootMarker is false
            : string.IsNullOrWhiteSpace(normalized);
        if (normalizedIsInvalidlyEmpty) {
            throw new ArgumentException("The archive name must not be empty.", nameof(path));
        }
        return normalized;
    }

    private static TarUpdateItem CreateUpdateItem(string path, string sourcePath) {
        if (sourcePath is null) {
            return new TarUpdateItem {
                Path = path,
                SourcePath = null,
                IsDirectory = true,
                ModifiedTime = null,
                Size = 0
            };
        }
        var directory = new DirectoryInfo(sourcePath);
        if (directory.Exists) {
            return new TarUpdateItem {
                Path = path,
                SourcePath = null,
                IsDirectory = true,
                ModifiedTime = new DateTimeOffset(directory.LastWriteTimeUtc, TimeSpan.Zero),
                Size = 0
            };
        }
        var file = new FileInfo(sourcePath);
        if (file.Exists is false) {
            throw new FileNotFoundException("The source file does not exist.", sourcePath);
        }
        return new TarUpdateItem {
            Path = path,
            SourcePath = sourcePath,
            IsDirectory = false,
            ModifiedTime = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
            Size = file.Length
        };
    }

    private static (IReadOnlySet<int> Removed, IReadOnlySet<int> Replaced, IReadOnlyList<TarUpdateItem> Items) PlanUpdate(
        IReadOnlyList<OriginalEntry> entries,
        IReadOnlyCollection<string> delete,
        IReadOnlyCollection<TarEntryInfo> deleteSelected,
        IReadOnlyDictionary<string, string> added) {
        /*
         * The items of an archive are rewritten when it is updated, so the
         * entries that are kept stay in the order that they hold in the
         * archive. The entries that are removed are dropped, and the entries
         * that are added replace the entries that use the same name.
         */
        var deleteNames = delete
            .Where(i => i is not null)
            .Select(NormalizeArchiveName)
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        var selectedEntries = deleteSelected
            .Where(entry => entry is not null)
            .Select(entry => (Name: NormalizeArchiveName(entry.Name.Original), Kind: entry.Kind))
            .ToHashSet();
        var selectedDirectories = selectedEntries
            .Where(entry => entry.Kind == TarEntryKind.Path)
            .Select(entry => entry.Name)
            .ToHashSet(StringComparer.Ordinal);
        var removed = new HashSet<int>();
        var replaced = new HashSet<int>();
        foreach (var entry in entries) {
            if (entry.Kind is null) {
                continue;
            }
            var normalizedPath = NormalizeArchiveName(entry.Name);
            var entryIsRemovedByName = deleteNames.Contains(normalizedPath) ||
                                       deleteNames.Any(name =>
                                           normalizedPath.StartsWith(name + "/", StringComparison.Ordinal));
            var entryIsRemovedBySelection = selectedEntries.Contains((normalizedPath, entry.Kind.Value));
            var entryIsDescendantOfSelectedDirectory = selectedDirectories.Any(directory =>
                normalizedPath.StartsWith(directory + "/", StringComparison.Ordinal));
            var entryIsRemoved = entryIsRemovedByName ||
                                 entryIsRemovedBySelection ||
                                 entryIsDescendantOfSelectedDirectory;
            if (entryIsRemoved) {
                removed.Add(entry.Index);
                continue;
            }
            var entryIsReplaced = added.ContainsKey(normalizedPath);
            if (entryIsReplaced) {
                replaced.Add(entry.Index);
            }
        }
        var items = added
            .Select(pair => CreateUpdateItem(pair.Key, pair.Value))
            .ToList();
        return (removed, replaced, items);
    }

    private static int IndexOf(IReadOnlyList<OriginalEntry> entries, string name) {
        /*
         * Listing keeps the last file for each normalized name, so nesting
         * resolves to the same occurrence even when separators differ.
         */
        var wanted = NormalizeArchiveName(name);
        for (var i = entries.Count - 1; i >= 0; i--) {
            var entry = entries[i];
            var entryIsNotAMatchableFile = entry.Kind is null ||
                                           entry.Kind == TarEntryKind.Path;
            if (entryIsNotAMatchableFile) {
                continue;
            }
            var entryName = NormalizeArchiveName(entry.Name);
            var nameIsEqual = string.Equals(entryName, wanted, StringComparison.Ordinal);
            if (nameIsEqual) {
                return i;
            }
        }
        return -1;
    }

    private static async Task ExtractOriginalEntry(string fileName,
                                                   OriginalEntry entry,
                                                   string destination,
                                                   IOperationProgress progress,
                                                   CancellationToken token) {
        await using (var stream = ArchiveStream(fileName)) {
            using var reader = new TarReader(stream);
            var index = 0;
            TarArchiveEntry archiveEntry;
            while ((archiveEntry = await reader.GetNextEntryAsync(cancellationToken: token)) is not null) {
                var archiveIndex = index++;
                var archiveIndexIsNotWanted = archiveIndex != entry.Index;
                var kind = EntryKindOf(archiveEntry);
                var entryIsNotAFile = kind is null || kind == TarEntryKind.Path;
                if (archiveIndexIsNotWanted || entryIsNotAFile) {
                    continue;
                }
                await ExtractDataTo(archiveEntry, destination, overwrite: true, progress, token);
                return;
            }
        }
        throw new IOException("The archive entry was not found.");
    }

    private static async Task ExtractDataTo(TarArchiveEntry entry,
                                            string destinationFile,
                                            bool overwrite,
                                            IOperationProgress progress,
                                            CancellationToken token) {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFile);
        var data = entry.DataStream;
        var destinationDirectory = Path.GetDirectoryName(destinationFile);
        var entryHasNoUsableData = data is null && IsEmptyDataEntry(entry) is false;
        var extractionIsInvalid = entryHasNoUsableData || string.IsNullOrEmpty(destinationDirectory);
        if (extractionIsInvalid) {
            throw new IOException("The archive entry does not hold file data.");
        }
        Directory.CreateDirectory(destinationDirectory);
        var staging = Path.Combine(destinationDirectory, $".brows-{Guid.NewGuid():N}.tmp");
        var stagingOwned = false;
        try {
            await using (var output = new FileStream(
                staging,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan)) {
                stagingOwned = true;
                progress?.Change(data: entry.Name);
                if (data is not null) {
                    await CopyStream(data, output, entry.Length, progress, token);
                }
                else {
                    progress?.Change(
                        addTarget: Math.Max(0, entry.Length),
                        kind: OperationProgressKind.FileSize);
                }
                await output.FlushAsync(token);
            }
            var modificationTime = entry.ModificationTime;
            var modificationTimeIsKnown = modificationTime != DateTimeOffset.MinValue;
            if (modificationTimeIsKnown) {
                System.IO.File.SetLastWriteTime(staging, modificationTime.LocalDateTime);
            }
            token.ThrowIfCancellationRequested();
            System.IO.File.Move(staging, destinationFile, overwrite);
            stagingOwned = false;
        }
        finally {
            var stagingFileExists = stagingOwned && System.IO.File.Exists(staging);
            if (stagingFileExists) {
                try {
                    System.IO.File.Delete(staging);
                }
                catch (Exception ex) {
                    if (Log.Error()) {
                        Log.Error(ex);
                    }
                }
            }
        }
    }

    private static async Task ExtractTo(TarArchiveEntry entry,
                                        string destination,
                                        bool overwrite,
                                        IOperationProgress progress,
                                        CancellationToken token) {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        if (entry.EntryType == TarEntryType.Directory) {
            Directory.CreateDirectory(destination);
            return;
        }
        var entryHasNoUsableData = entry.DataStream is null && IsEmptyDataEntry(entry) is false;
        if (entryHasNoUsableData) {
            /*
             * A link, device, or fifo entry does not hold file data. Its
             * content is listed, but it is not extracted to a file.
             */
            throw new IOException("The archive entry does not hold file data.");
        }
        await ExtractDataTo(entry, destination, overwrite, progress, token);
    }

    private static async Task WriteItems(TarWriter writer,
                                         IReadOnlyList<TarUpdateItem> items,
                                         IOperationProgress progress,
                                         CancellationToken token) {
        ArgumentNullException.ThrowIfNull(writer);
        if (items is null) {
            return;
        }
        foreach (var item in items) {
            token.ThrowIfCancellationRequested();
            if (item.IsDirectory) {
                var directoryEntry = new PaxTarEntry(TarEntryType.Directory, item.Path);
                if (item.ModifiedTime is DateTimeOffset directoryTime) {
                    directoryEntry.ModificationTime = directoryTime;
                }
                if (progress is not null) {
                    progress.Change(data: item.Path);
                }
                await writer.WriteEntryAsync(directoryEntry);
                continue;
            }
            await using (var data = ArchiveStream(item.SourcePath)) {
                var fileEntry = new PaxTarEntry(TarEntryType.RegularFile, item.Path);
                if (item.ModifiedTime is DateTimeOffset fileTime) {
                    fileEntry.ModificationTime = fileTime;
                }
                fileEntry.DataStream = data;
                if (progress is not null) {
                    progress.Change(addTarget: item.Size, data: item.Path);
                }
                await writer.WriteEntryAsync(fileEntry);
                if (progress is not null) {
                    progress.Change(addProgress: item.Size);
                }
            }
        }
    }

    private static async Task WriteArchive(string sourceFile,
                                           string destinationFile,
                                           bool archiveExists,
                                           IReadOnlySet<int> removed,
                                           IReadOnlySet<int> replaced,
                                           int? replacementIndex,
                                           string replacementFile,
                                           IReadOnlyList<TarUpdateItem> items,
                                           IOperationProgress progress,
                                           CancellationToken token) {
        ArgumentNullException.ThrowIfNull(sourceFile);
        ArgumentNullException.ThrowIfNull(destinationFile);
        ArgumentNullException.ThrowIfNull(removed);
        ArgumentNullException.ThrowIfNull(replaced);
        ArgumentNullException.ThrowIfNull(items);
        if (Log.Info()) {
            Log.Info(nameof(WriteArchive) + " > " + sourceFile);
        }
        var parentDirectory = Path.GetDirectoryName(Path.GetFullPath(destinationFile));
        if (string.IsNullOrEmpty(parentDirectory)) {
            throw new ArgumentException("The destination has no parent directory.", nameof(destinationFile));
        }
        var stagingFile = Path.Combine(parentDirectory, $".brows-{Guid.NewGuid():N}.tmp");
        var stagingOwned = false;
        try {
            await using (var output = ArchiveStream(stagingFile, FileMode.CreateNew, FileAccess.Write)) {
                stagingOwned = true;
                await using (var writer = new TarWriter(output, leaveOpen: true)) {
                    if (archiveExists) {
                        await using (var input = ArchiveStream(sourceFile)) {
                            using (var reader = new TarReader(input)) {
                                var index = 0;
                                TarArchiveEntry entry;
                                while ((entry = await reader.GetNextEntryAsync(cancellationToken: token)) is not null) {
                                    token.ThrowIfCancellationRequested();
                                    var entryIndex = index++;
                                    var entryIsDropped = removed.Contains(entryIndex) ||
                                                         replaced.Contains(entryIndex);
                                    if (entryIsDropped) {
                                        continue;
                                    }
                                    var entryIsReplaced = replacementIndex is int wanted &&
                                                          entryIndex == wanted;
                                    if (entryIsReplaced) {
                                        await using (var replacement = ArchiveStream(replacementFile)) {
                                            entry.DataStream = replacement;
                                            if (progress is not null) {
                                                progress.Change(addTarget: replacement.Length, data: entry.Name);
                                            }
                                            await writer.WriteEntryAsync(entry);
                                            if (progress is not null) {
                                                progress.Change(addProgress: replacement.Length);
                                            }
                                        }
                                        continue;
                                    }
                                    var entryIsMetadata = EntryKindOf(entry) is null &&
                                                          entry.EntryType is not TarEntryType.GlobalExtendedAttributes;
                                    if (entryIsMetadata) {
                                        continue;
                                    }
                                    if (progress is not null) {
                                        progress.Change(addTarget: entry.Length, data: entry.Name);
                                    }
                                    await writer.WriteEntryAsync(entry);
                                    if (progress is not null) {
                                        progress.Change(addProgress: entry.Length);
                                    }
                                }
                            }
                        }
                    }
                    await WriteItems(writer, items, progress, token);
                }
                await output.FlushAsync(token);
            }
            token.ThrowIfCancellationRequested();
            System.IO.File.Move(stagingFile, destinationFile, overwrite: true);
            stagingOwned = false;
        }
        finally {
            var stagingFileExists = stagingOwned && System.IO.File.Exists(stagingFile);
            if (stagingFileExists) {
                try {
                    System.IO.File.Delete(stagingFile);
                }
                catch (Exception ex) {
                    if (Log.Error()) {
                        Log.Error(ex);
                    }
                }
            }
        }
    }

    /*
     * Tests use this fallback when no composition scope exists. Runtime
     * callers resolve the archive identity asynchronously before opening it.
     */
    internal static TarArchivePath GetForTest(FileInfo file) {
        ArgumentNullException.ThrowIfNull(file);
        return Get(TarArchiveIdentity.CreateFallback(file), new TarArchiveNest([]));
    }

    internal static TarArchivePath Get(TarArchiveIdentity identity) {
        return Get(identity, new TarArchiveNest([]));
    }

    internal static TarArchivePath Get(TarArchiveIdentity identity, TarArchiveNest nest) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(nest);
        var key = (Identity: identity, Nest: new NestKey(nest));
        lock (SetLocker) {
            if (Set.TryGetValue(key, out var value) is false) {
                Set[key] = value = new TarArchivePath(identity, nest);
            }
            return value.Referenced();
        }
    }

    private async Task<List<OriginalEntry>> ListOriginalEntries(string fileName, CancellationToken token) {
        var entries = new List<OriginalEntry>();
        using (var stream = ArchiveStream(fileName)) {
            using var reader = new TarReader(stream);
            var index = 0;
            TarArchiveEntry archiveEntry;
            while ((archiveEntry = reader.GetNextEntry()) is not null) {
                token.ThrowIfCancellationRequested();
                if (Log.Info()) {
                    Log.Info(nameof(archiveEntry) + " > " + archiveEntry.Name);
                }
                var modificationTime = archiveEntry.ModificationTime;
                var modificationTimeIsKnown = modificationTime != DateTimeOffset.MinValue;
                entries.Add(new OriginalEntry {
                    Index = index++,
                    Name = archiveEntry.Name,
                    Kind = EntryKindOf(archiveEntry),
                    Size = archiveEntry.Length,
                    ModifiedTime = modificationTimeIsKnown ? modificationTime : null,
                });
            }
        }
        return entries;
    }

    private TarEntryInfoCollection ListEntries(string fileName, CancellationToken token) {
        var fileSet = new Dictionary<string, TarEntryInfo>();
        var pathSet = new Dictionary<string, TarEntryInfo>();
        using (var stream = ArchiveStream(fileName)) {
            using var reader = new TarReader(stream);
            TarArchiveEntry archiveEntry;
            while ((archiveEntry = reader.GetNextEntry()) is not null) {
                token.ThrowIfCancellationRequested();
                if (Log.Info()) {
                    Log.Info(nameof(archiveEntry) + " > " + archiveEntry.Name);
                }
                var kind = EntryKindOf(archiveEntry);
                if (kind is null) {
                    continue;
                }
                var key = new TarEntryName(archiveEntry.Name);
                if (kind == TarEntryKind.Path) {
                    if (key.Normalized.Length == 0) {
                        continue;
                    }
                    pathSet[key.Normalized] = TarEntryInfo.Path(this, key);
                }
                else {
                    var lastWriteTime = archiveEntry.ModificationTime;
                    var lastWriteTimeIsKnown = lastWriteTime != DateTimeOffset.MinValue;
                    fileSet[key.Normalized] = TarEntryInfo.File(
                        this,
                        key,
                        archiveEntry.Length,
                        lastWriteTimeIsKnown ? lastWriteTime : null);
                }
            }
        }
        /*
         * An archive does not have to hold the folders of its files.
         * The folders that are missing are added, so that the items
         * of an archive can always be browsed by folder.
         */
        var entries = pathSet.Values.Concat(fileSet.Values).ToList();
        foreach (var entryInfo in entries) {
            token.ThrowIfCancellationRequested();
            foreach (var path in entryInfo.Name.Paths) {
                token.ThrowIfCancellationRequested();
                if (pathSet.TryGetValue(path, out _) is false) {
                    pathSet[path] = TarEntryInfo.Path(this, new TarEntryName(path));
                }
            }
        }
        return new TarEntryInfoCollection(
            items: pathSet.Values.Concat(fileSet.Values));
    }

    private async Task<Stage> OpenInnermost(IOperationProgress progress, CancellationToken token) {
        if (Log.Info()) {
            Log.Info(nameof(OpenInnermost) + " > " + FullName);
        }
        var stage = new Stage(File.FullName);
        try {
            var level = File.FullName;
            foreach (var nested in Nest) {
                token.ThrowIfCancellationRequested();
                var entries = await ListOriginalEntries(level, token);
                var index = IndexOf(entries, nested);
                var nestedNotFound = index < 0;
                if (nestedNotFound) {
                    throw new FileNotFoundException("The nested archive was not found.", nested);
                }
                var staged = stage.CreateFile();
                var entry = entries[index];
                if (progress is null) {
                    await ExtractOriginalEntry(level, entry, staged, progress, token);
                }
                else {
                    await RunArchiveOperation(
                        nested,
                        progress,
                        (childProgress, childToken) => ExtractOriginalEntry(level, entry, staged, childProgress, childToken),
                        token);
                }
                level = staged;
            }
            return stage;
        }
        catch {
            stage.Dispose();
            throw;
        }
    }

    private async Task ReadFrom(string fileName,
                                IReadOnlyDictionary<string, string> extract,
                                bool overwrite,
                                IOperationProgress progress,
                                CancellationToken token) {
        /*
         * Find the last matching occurrence before extracting. Exact stored
         * names take precedence over normalized matches, while duplicates of
         * either form resolve to their last occurrence.
         */
        var requested = new HashSet<string>(extract.Keys, StringComparer.Ordinal);
        var normalizedNames = requested
            .GroupBy(key => new TarEntryName(key).Normalized, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var exactIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        var normalizedIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        using (var stream = ArchiveStream(fileName)) {
            using var reader = new TarReader(stream);
            var index = 0;
            TarArchiveEntry entry;
            while ((entry = await reader.GetNextEntryAsync(cancellationToken: token)) is not null) {
                token.ThrowIfCancellationRequested();
                var archiveIndex = index++;
                var kind = EntryKindOf(entry);
                if (kind is null) {
                    continue;
                }
                var name = new TarEntryName(entry.Name);
                var requestedName = requested.Contains(entry.Name)
                    ? entry.Name
                    : normalizedNames.TryGetValue(name.Normalized, out var match) &&
                      requested.Contains(match)
                        ? match
                        : null;
                if (requestedName is null) {
                    continue;
                }
                var entryNameIsExact = string.Equals(entry.Name, requestedName, StringComparison.Ordinal);
                if (entryNameIsExact) {
                    exactIndices[requestedName] = archiveIndex;
                }
                else {
                    normalizedIndices[requestedName] = archiveIndex;
                }
            }
        }
        var indicesToExtract = new Dictionary<int, string>();
        foreach (var name in requested) {
            var hasExactMatch = exactIndices.TryGetValue(name, out var index);
            var hasNormalizedMatch = normalizedIndices.TryGetValue(name, out var normalizedIndex);
            if (hasExactMatch) {
                indicesToExtract[index] = name;
                continue;
            }
            if (hasNormalizedMatch) {
                indicesToExtract[normalizedIndex] = name;
            }
        }
        using (var stream = ArchiveStream(fileName)) {
            using var reader = new TarReader(stream);
            var index = 0;
            TarArchiveEntry entry;
            while ((entry = await reader.GetNextEntryAsync(cancellationToken: token)) is not null) {
                token.ThrowIfCancellationRequested();
                var archiveIndex = index++;
                if (indicesToExtract.TryGetValue(archiveIndex, out var requestedName) is false) {
                    continue;
                }
                requested.Remove(requestedName);
                var destination = extract[requestedName];
                if (progress is not null) {
                    progress.Change(data: requestedName);
                }
                await RunArchiveOperation(
                    requestedName,
                    progress,
                    (childProgress, childToken) => ExtractTo(entry, destination, overwrite, childProgress, childToken),
                    token);
            }
        }
        if (progress is not null) {
            foreach (var name in requested) {
                progress.Change(data: name);
            }
        }
    }

    private async Task ExtractInnermostAll(string directory,
                                           bool overwrite,
                                           IOperationProgress progress,
                                           CancellationToken token) {
        var root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        using (var stage = await OpenInnermost(progress, token)) {
            using (var stream = ArchiveStream(stage.Innermost)) {
                using var reader = new TarReader(stream);
                TarArchiveEntry entry;
                while ((entry = await reader.GetNextEntryAsync(cancellationToken: token)) is not null) {
                    token.ThrowIfCancellationRequested();
                    var kind = EntryKindOf(entry);
                    if (kind is null) {
                        continue;
                    }
                    var name = new TarEntryName(entry.Name);
                    var normalizedName = NormalizeArchiveEntryRelativePath(name.Original);
                    if (kind == TarEntryKind.Path) {
                        if (normalizedName.Length > 0) {
                            OutputPath.CreateDirectory(root, normalizedName);
                        }
                        continue;
                    }
                    if (normalizedName.Length == 0) {
                        throw new InvalidDataException("The archive entry name must not be empty.");
                    }
                    var entryHasNoUsableData = entry.DataStream is null && IsEmptyDataEntry(entry) is false;
                    if (entryHasNoUsableData) {
                        /*
                         * A link, device, or fifo entry does not hold file
                         * data. Its content is listed, but it is not
                         * extracted to a file.
                         */
                        continue;
                    }
                    var parent = new TarEntryName(normalizedName).Parent;
                    if (parent.Length > 0) {
                        OutputPath.CreateDirectory(root, parent);
                    }
                    var destination = OutputPath.GetPath(root, normalizedName);
                    if (progress is not null) {
                        progress.Change(data: normalizedName);
                    }
                    await ExtractDataTo(entry, destination, overwrite, progress, token);
                }
            }
        }
    }

    private void RemoveTemporary(IReadOnlyCollection<string> temporary) {
        foreach (var file in temporary) {
            try {
                new FileInfo(file).Delete();
                var directory = Path.GetDirectoryName(file);
                if (directory is not null) {
                    Directory.Delete(directory);
                }
            }
            catch (Exception ex) {
                if (Log.Error()) {
                    Log.Error(ex);
                }
            }
        }
    }

    private int ReferenceCount;
    private DateTime LastWriteTimeUtc;

    private readonly TarArchiveIdentity Identity;
    private readonly TarArchiveLocker Locker;
    private readonly IDisposable FileEvent;

    private TaskCache<TarEntryInfoCollection> EntryInfoCache {
        get => field ??= new(EntryInfoLoad);
        set;
    }

    private TarArchivePath(TarArchiveIdentity identity, TarArchiveNest nest) {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Nest = nest ?? throw new ArgumentNullException(nameof(nest));
        File = new FileInfo(identity.FullName);
        FullName = MakeFullName(identity, nest);
        FileEvent = FileSystemEventTasks.Add(File.DirectoryName, FileEventTask);
        Locker = TarArchiveLocker.Get(identity);
        LastWriteTimeUtc = File.LastWriteTimeUtc;
    }

    private async Task<TarEntryInfoCollection> EntryInfoLoad(CancellationToken token) {
        if (Log.Info()) {
            Log.Info(nameof(Read) + " > " + FullName);
        }
        var locked = Locker.Lock(token);
        using (await locked) {
            async Task<TarEntryInfoCollection> work() {
                using (var stage = await OpenInnermost(progress: null, token)) {
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    var innermost = stage.Innermost;
                    return ListEntries(innermost, token);
                }
            }
            return await Task.Run(work, token);
        }
    }

    private TarArchivePath Referenced() {
        if (Log.Info()) {
            Log.Info(nameof(Referenced) + " > " + FullName + " > " + ReferenceCount);
        }
        ReferenceCount++;
        return this;
    }

    private async Task FileEventTask(FileSystemEventArgs e, CancellationToken token) {
        if (e is not null) {
            var name = File.Name;
            var names = e is RenamedEventArgs r
                ? new[] { e.Name, r.OldName }
                : new[] { e.Name };
            if (names.Contains(name, StringComparer.OrdinalIgnoreCase)) {
                var file = File.FullName;
                var update = await FileSystemTask.ExistingFile(file, token);
                if (update is null) {
                    EntryInfoCache = null;
                    await FileDeleted.All(token);
                }
                else {
                    if (LastWriteTimeUtc != update.LastWriteTimeUtc) {
                        LastWriteTimeUtc = update.LastWriteTimeUtc;
                        EntryInfoCache = null;
                        await FileChanged.All(token);
                    }
                }
            }
        }
    }

    public TarArchiveFileEvent FileChanged => field ??= new();

    public TarArchiveFileEvent FileDeleted => field ??= new();

    public string FullName { get; }

    public string Parent {
        get {
            if (field is null) {
                if (Nest.Count == 0) {
                    return field = File.DirectoryName;
                }
                var nesting = Nest.ToList();
                var lastMember = nesting[^1];
                nesting.RemoveAt(nesting.Count - 1);
                var parentLocation = new ArchiveProviderID(
                    ArchiveProviderIDFormat.Tar,
                    File.FullName,
                    nesting,
                    new TarEntryName(lastMember).Parent);
                field = ArchiveProviderIDCodec.Format(parentLocation);
            }
            return field;
        }
    }

    public string FileName =>
        File.FullName;

    public FileInfo File { get; }

    public TarArchiveNest Nest { get; }

    public static async Task<TarArchivePath> GetAsync(FileInfo file, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(file);
        var identity = await TarArchiveIdentity.Create(file, token);
        return Get(identity, new TarArchiveNest([]));
    }

    public void Release() {
        lock (SetLocker) {
            if (Log.Info()) {
                Log.Info(nameof(Release) + " > " + FullName);
            }
            var count = --ReferenceCount;
            if (count == 0) {
                if (Log.Info()) {
                    Log.Info(nameof(Set.Remove));
                }
                Set.Remove((Identity, new NestKey(Nest)));
                Locker.Release();
                using (FileEvent) {
                    FileChanged.Clear();
                    FileDeleted.Clear();
                }
            }
            if (Log.Info()) {
                Log.Info(nameof(ReferenceCount) + " > " + ReferenceCount);
            }
        }
    }

    public TarEntryInfoCollection EntryInfo() {
        return EntryInfoCache.Result;
    }

    public Task<TarEntryInfoCollection> EntryInfo(CancellationToken token) {
        return EntryInfoCache.Ready(token);
    }

    public async Task Read(TarArchiveRead info, IOperationProgress progress, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(info);
        if (Log.Info()) {
            Log.Info(nameof(Read) + " > " + FullName);
        }
        var extract = new Dictionary<string, string>(info.ExtractEntriesToFiles);
        if (extract.Count == 0) {
            return;
        }
        progress?.Change(kind: OperationProgressKind.FileSize);
        var locked = Locker.Lock(token);
        using (await locked) {
            async Task work() {
                using (var stage = await OpenInnermost(progress, token)) {
                    await ReadFrom(stage.Innermost, extract, info.ExtractOverwrites, progress, token);
                }
            }
            await Task.Run(work, token);
        }
    }

    public async Task ExtractAll(string directory,
                                 bool overwrite,
                                 IOperationProgress progress,
                                 CancellationToken token) {
        ArgumentNullException.ThrowIfNull(directory);
        if (Log.Info()) {
            Log.Info(nameof(ExtractAll) + " > " + FullName);
        }
        progress?.Change(kind: OperationProgressKind.FileSize);
        var locked = Locker.Lock(token);
        using (await locked) {
            await RunArchiveOperation(
                Path.GetFileName(File.FullName),
                progress,
                (childProgress, childToken) => ExtractInnermostAll(directory, overwrite, childProgress, childToken),
                token);
        }
    }

    public async Task Update(TarArchiveUpdate info, IOperationProgress progress, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(info);
        if (Log.Info()) {
            Log.Info(nameof(Update) + " > " + FullName);
        }
        var delete = new HashSet<string>(info.DeleteEntries.Where(i => i is not null));
        var deleteSelected = info.DeleteSelectedEntries
            .Where(entry => entry is not null)
            .ToList();
        var create = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in info.CreateEntriesFromFiles) {
            ArgumentNullException.ThrowIfNull(pair.Value);
            create[NormalizeArchiveRelativePath(pair.Key)] = pair.Value;
        }
        var stream = new List<IEntryStreamSet>(info.CopyStreamSets.Where(i => i is not null));
        var copyStreamTargetDirectory = string.IsNullOrWhiteSpace(info.CopyStreamTargetDirectory)
            ? null
            : NormalizeArchiveRelativePath(info.CopyStreamTargetDirectory.Trim());
        var streamIsEmpty = stream.Count == 0;
        var createIsEmpty = create.Count == 0;
        var deleteIsEmpty = delete.Count == 0;
        var deleteSelectedIsEmpty = deleteSelected.Count == 0;
        var updateIsEmpty = streamIsEmpty && createIsEmpty && deleteIsEmpty && deleteSelectedIsEmpty;
        if (updateIsEmpty) {
            return;
        }
        progress?.Change(kind: OperationProgressKind.FileSize);
        var added = new Dictionary<string, string>(create);
        var temporary = new List<string>();
        try {
            foreach (var set in stream) {
                await set.Consume(
                    progress: progress,
                    token: token,
                    consuming: async (source, content, progress, token) => {
                        var relativePath = source.RelativePath;
                        ArgumentNullException.ThrowIfNull(relativePath);
                        var name = NormalizeArchiveRelativePath(relativePath.Trim());
                        if (copyStreamTargetDirectory is not null) {
                            name = NormalizeArchiveRelativePath($"{copyStreamTargetDirectory}/{name}");
                        }
                        if (content is null) {
                            if (source.SourceDirectory is null) {
                                return;
                            }
                            added[name] = null;
                            if (progress is not null) {
                                progress.Change(data: name);
                            }
                            return;
                        }
                        var directory = Path.Combine(
                            Path.GetTempPath(),
                            "Brows",
                            "Tar",
                            Guid.NewGuid().ToString("N"));
                        while (Directory.Exists(directory)) {
                            directory = Path.Combine(
                                Path.GetTempPath(),
                                "Brows",
                                "Tar",
                                Guid.NewGuid().ToString("N"));
                        }
                        Directory.CreateDirectory(directory);
                        var file = Path.Combine(directory, Guid.NewGuid().ToString("N"));
                        await using (var output = new FileStream(
                            file,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.None)) {
                            temporary.Add(file);
                            await CopyStream(content, output, source.StreamLength, progress, token);
                        }
                        added[name] = file;
                        if (progress is not null) {
                            progress.Change(data: name);
                        }
                    });
            }
            var locked = Locker.Lock(token);
            using (await locked) {
                var operationName = Path.GetFileName(File.FullName);
                if (Nest.Count == 0) {
                    await WriteOutermost(added, delete, deleteSelected, operationName, progress, token);
                }
                else {
                    await WriteNested(added, delete, deleteSelected, operationName, progress, token);
                }
                /*
                 * The archive was written, so the listing of its items is
                 * read again the next time that it is asked for.
                 */
                EntryInfoCache = null;
                if (progress is not null) {
                    foreach (var name in delete) {
                        progress.Change(data: name);
                    }
                    foreach (var entry in deleteSelected) {
                        progress.Change(data: entry.Name.Original);
                    }
                    foreach (var name in create.Keys) {
                        progress.Change(data: name);
                    }
                }
            }
        }
        finally {
            RemoveTemporary(temporary);
        }
    }

    private async Task WriteOutermost(IReadOnlyDictionary<string, string> added,
                                      IReadOnlyCollection<string> delete,
                                      IReadOnlyCollection<TarEntryInfo> deleteSelected,
                                      string operationName,
                                      IOperationProgress progress,
                                      CancellationToken token) {
        File.Refresh();
        var archiveExists = File.Exists;
        var entries = archiveExists
            ? await ListOriginalEntries(File.FullName, token)
            : new List<OriginalEntry>();
        var (removed, replaced, items) = PlanUpdate(entries, delete, deleteSelected, added);
        if (Log.Info()) {
            Log.Info(nameof(WriteOutermost) + " > " + FullName);
        }
        await RunArchiveOperation(
            operationName,
            progress,
            (childProgress, childToken) => WriteArchive(
                File.FullName,
                File.FullName,
                archiveExists,
                removed,
                replaced,
                replacementIndex: null,
                replacementFile: null,
                items,
                childProgress,
                childToken),
            token);
    }

    private async Task WriteNested(IReadOnlyDictionary<string, string> added,
                                   IReadOnlyCollection<string> delete,
                                   IReadOnlyCollection<TarEntryInfo> deleteSelected,
                                   string operationName,
                                   IOperationProgress progress,
                                   CancellationToken token) {
        if (Log.Info()) {
            Log.Info(nameof(WriteNested) + " > " + FullName);
        }
        using (var stage = await OpenInnermost(progress, token)) {
            var filesByLevel = stage.Files.ToList();
            var entriesByLevel = new List<IReadOnlyList<OriginalEntry>>();
            foreach (var file in filesByLevel) {
                entriesByLevel.Add(await ListOriginalEntries(file, token));
            }
            var (removed, replaced, items) = PlanUpdate(entriesByLevel[^1], delete, deleteSelected, added);
            var nestNames = Nest.ToList();
            var updated = filesByLevel[^1];
            await RunArchiveOperation(
                operationName,
                progress,
                (childProgress, childToken) => WriteArchive(
                    filesByLevel[^1],
                    filesByLevel[^1],
                    true,
                    removed,
                    replaced,
                    replacementIndex: null,
                    replacementFile: null,
                    items,
                    childProgress,
                    childToken),
                token);
            /*
             * An archive that is entered through a nesting is held by the
             * archive above it. The archives of the levels above are updated
             * with the replaced item, in order, out to the archive of the
             * file system.
             */
            for (var level = entriesByLevel.Count - 2; level >= 0; level--) {
                token.ThrowIfCancellationRequested();
                var nestedName = nestNames[level];
                var entries = entriesByLevel[level];
                var index = IndexOf(entries, nestedName);
                var nestedNotFound = index < 0;
                if (nestedNotFound) {
                    throw new FileNotFoundException("The nested archive was not found.", nestedName);
                }
                await RunArchiveOperation(
                    operationName,
                    progress,
                    (childProgress, childToken) => WriteArchive(
                        filesByLevel[level],
                        filesByLevel[level],
                        true,
                        new HashSet<int>(),
                        new HashSet<int>(),
                        entries[index].Index,
                        updated,
                        new List<TarUpdateItem>(),
                        childProgress,
                        childToken),
                    token);
                updated = filesByLevel[level];
            }
        }
    }

    public abstract class StreamSource<TEntry> : EntryStreamSource<TEntry> where TEntry : IEntry {
        private string TemporaryDirectory;
        private string TemporaryFile;
        private FileStream FileStream;

        private void CleanupTemporary() {
            try {
                FileStream?.Dispose();
            }
            catch (Exception ex) {
                if (Log.Error()) {
                    Log.Error(ex);
                }
            }
            FileStream = null;
            Stream = null;
            var temporary = TemporaryFile;
            TemporaryFile = null;
            if (temporary is not null) {
                try {
                    new FileInfo(temporary).Delete();
                }
                catch (Exception ex) {
                    if (Log.Error()) {
                        Log.Error(ex);
                    }
                }
            }
            var directory = TemporaryDirectory;
            TemporaryDirectory = null;
            if (directory is not null) {
                try {
                    Directory.Delete(directory);
                }
                catch (Exception ex) {
                    if (Log.Error()) {
                        Log.Error(ex);
                    }
                }
            }
        }

        protected TarEntryInfo EntryInfo { get; }

        protected StreamSource(TEntry entry, TarEntryInfo entryInfo) : base(entry) {
            EntryInfo = entryInfo ?? throw new ArgumentNullException(nameof(entryInfo));
        }

        protected sealed override async Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            if (EntryInfo.Kind == TarEntryKind.File) {
                NormalizeArchiveRelativePath(EntryInfo.Name.Original);
                /*
                 * This adapter stages the entry in a temporary file and owns that file until
                 * the returned stream-ready lease is disposed.
                 */
                var directory = Path.Combine(
                    Path.GetTempPath(),
                    "Brows",
                    "Tar",
                    Guid.NewGuid().ToString("N"));
                while (Directory.Exists(directory)) {
                    directory = Path.Combine(
                        Path.GetTempPath(),
                        "Brows",
                        "Tar",
                        Guid.NewGuid().ToString("N"));
                }
                Directory.CreateDirectory(directory);
                TemporaryDirectory = directory;
                var temporary = Path.Combine(directory, Guid.NewGuid().ToString("N"));
                try {
                    using (var reservation = new FileStream(
                        temporary,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None)) {
                        TemporaryFile = temporary;
                    }
                    await EntryInfo.Extract(temporary, overwrite: true, token);
                    FileStream = new FileStream(
                        temporary,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read);
                    Stream = FileStream;
                }
                catch {
                    CleanupTemporary();
                    throw;
                }
            }
            return new Disposable(this);
        }

        public sealed override long StreamLength =>
            EntryInfo.SizeOriginal ?? 0;

        private sealed class Disposable : IEntryStreamReady {
            public StreamSource<TEntry> Source { get; }

            public Disposable(StreamSource<TEntry> source) {
                Source = source ?? throw new ArgumentNullException(nameof(source));
            }

            void IDisposable.Dispose() {
                Source.CleanupTemporary();
            }
        }
    }

    /*
     * The names of the entries of an archive are validated before they are
     * used to build an output path, so that a crafted archive cannot write
     * outside of the directory that it is extracted to.
     */
    private static class OutputPath {
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

        internal static string ValidateRelativePath(string relativePath) {
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
            return pathToValidate;
        }

        internal static string GetPath(string root, string relativePath) {
            if (root is null) {
                throw new ArgumentNullException(nameof(root));
            }
            var path = ValidateRelativePath(relativePath);
            var fullRoot = Path.GetFullPath(root);
            var fullPath = Path.GetFullPath(Path.Combine(fullRoot, path));
            EnsureContained(fullRoot, fullPath);
            return fullPath;
        }

        internal static string CreateDirectory(string root, string relativePath) {
            var path = GetPath(root, relativePath);
            EnsureNoReparsePoints(root, path);
            Directory.CreateDirectory(path);
            EnsureNoReparsePoints(root, path);
            return path;
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
                    attributes = System.IO.File.GetAttributes(currentPath);
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

    private record OriginalEntry {
        internal int Index { get; init; }

        internal string Name { get; init; }

        internal TarEntryKind? Kind { get; init; }

        internal long Size { get; init; }

        internal DateTimeOffset? ModifiedTime { get; init; }

        internal bool IsDirectory =>
            Kind == TarEntryKind.Path;
    }

    private record TarUpdateItem {
        internal string Path { get; init; }

        internal string SourcePath { get; init; }

        internal bool IsDirectory { get; init; }

        internal DateTimeOffset? ModifiedTime { get; init; }

        internal long Size { get; init; }
    }

    private sealed class Stage : IDisposable {
        private readonly List<string> FileList = [];
        private readonly List<string> DirectoryList = [];

        public Stage(string initialFile) {
            FileList.Add(initialFile ?? throw new ArgumentNullException(nameof(initialFile)));
        }

        public IReadOnlyList<string> Files =>
            FileList;

        public string Innermost =>
            FileList[^1];

        public string CreateFile() {
            for (; ; ) {
                var directory = Path.Combine(
                    Path.GetTempPath(),
                    "Brows",
                    "Tar",
                    Guid.NewGuid().ToString("N"));
                var directoryExists = Directory.Exists(directory);
                if (directoryExists) {
                    continue;
                }
                Directory.CreateDirectory(directory);
                DirectoryList.Add(directory);
                var file = Path.Combine(directory, Guid.NewGuid().ToString("N"));
                using (var reservation = new FileStream(
                    file,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None)) {
                }
                FileList.Add(file);
                return file;
            }
        }

        public void Dispose() {
            for (var i = FileList.Count - 1; i > 0; i--) {
                try {
                    System.IO.File.Delete(FileList[i]);
                }
                catch (Exception ex) {
                    if (Log.Error()) {
                        Log.Error(ex);
                    }
                }
            }
            FileList.Clear();
            foreach (var directory in DirectoryList) {
                try {
                    Directory.Delete(directory);
                }
                catch (Exception ex) {
                    if (Log.Error()) {
                        Log.Error(ex);
                    }
                }
            }
            DirectoryList.Clear();
        }
    }
}
