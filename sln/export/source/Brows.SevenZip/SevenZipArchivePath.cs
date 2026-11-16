using Brows.SevenZip;
using Brows.Shared.Archives;
using Domore.IO;
using Domore.Logs;
using Domore.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class SevenZipArchivePath {
    private static readonly ILog Log = Logging.For(typeof(SevenZipArchivePath));
    private static readonly Lock SetLocker = new();
    private static readonly Dictionary<(SevenZipArchiveIdentity Identity, NestKey Nest), SevenZipArchivePath> Set =
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

    private static string MakeFullName(SevenZipArchiveIdentity identity, SevenZipArchiveNest nest) {
        return ArchiveProviderIDCodec.Format(new ArchiveProviderID(
            ArchiveProviderIDFormat.SevenZip,
            identity.FullName,
            nest,
            ""));
    }

    private int ReferenceCount;
    private DateTime LastWriteTimeUtc;

    private readonly SevenZipArchiveIdentity Identity;
    private readonly SevenZipArchiveLocker Locker;
    private readonly IDisposable FileEvent;

    private TaskCache<SevenZipEntryInfoCollection> EntryInfoCache {
        get => field ??= new(EntryInfoLoad);
        set;
    }

    private SevenZipArchivePath(SevenZipArchiveIdentity identity, SevenZipArchiveNest nest) {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Nest = nest ?? throw new ArgumentNullException(nameof(nest));
        File = new FileInfo(identity.FullName);
        FullName = MakeFullName(identity, nest);
        FileEvent = FileSystemEventTasks.Add(File.DirectoryName, FileEventTask);
        Locker = SevenZipArchiveLocker.Get(identity);
        LastWriteTimeUtc = File.LastWriteTimeUtc;
    }

    private async Task<SevenZipEntryInfoCollection> EntryInfoLoad(CancellationToken token) {
        if (Log.Info()) {
            Log.Info(nameof(Read) + " > " + FullName);
        }
        var locked = Locker.Lock(token);
        using (await locked) {
            async Task<SevenZipEntryInfoCollection> work() {
                using (var stage = await OpenInnermost(progress: null, token)) {
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    var archive = stage.Innermost;
                    var fileSet = new Dictionary<string, SevenZipEntryInfo>();
                    var pathSet = new Dictionary<string, SevenZipEntryInfo>();
                    foreach (var entry in archive.Entries) {
                        if (token.IsCancellationRequested) {
                            token.ThrowIfCancellationRequested();
                        }
                        if (Log.Info()) {
                            Log.Info(nameof(entry) + " > " + entry.Path);
                        }
                        var key = new SevenZipEntryName(entry.Path);
                        if (entry.IsDirectory) {
                            pathSet[key.Normalized] = SevenZipEntryInfo.Path(this, key);
                        }
                        else {
                            fileSet[key.Normalized] = SevenZipEntryInfo.File(this, key, entry);
                        }
                    }
                    /*
                     * An archive does not have to hold the folders of its files.
                     * The folders that are missing are added, so that the items
                     * of an archive can always be browsed by folder.
                     */
                    var entries = pathSet.Values.Concat(fileSet.Values).ToList();
                    foreach (var entryInfo in entries) {
                        if (token.IsCancellationRequested) {
                            token.ThrowIfCancellationRequested();
                        }
                        foreach (var path in entryInfo.Name.Paths) {
                            if (token.IsCancellationRequested) {
                                token.ThrowIfCancellationRequested();
                            }
                            if (pathSet.TryGetValue(path, out _) is false) {
                                pathSet[path] = SevenZipEntryInfo.Path(this, new SevenZipEntryName(path));
                            }
                        }
                    }
                    return new SevenZipEntryInfoCollection(
                        items: pathSet.Values.Concat(fileSet.Values));
                }
            }
            return await Task.Run(work, token);
        }
    }

    private SevenZipArchivePath Referenced() {
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

    private static string NormalizeArchiveRelativePath(string path) {
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
        var normalized = path
            .Trim('/', '\\')
            .Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized)) {
            throw new ArgumentException("The archive name must not be empty.", nameof(path));
        }
        return normalized;
    }

    private static string NormalizeArchiveSeparators(string path) {
        return path.Replace('\\', '/');
    }

    internal static async Task RunArchiveOperation(string name,
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

    private static SevenZipUpdateItem CreateUpdateItem(string path, string sourcePath) {
        if (sourcePath is null) {
            return new SevenZipUpdateItem(
                path: path,
                sourcePath: null,
                isDirectory: true,
                size: 0,
                modifiedTime: null,
                attributes: 0);
        }
        var directory = new DirectoryInfo(sourcePath);
        if (directory.Exists) {
            return new SevenZipUpdateItem(
                path: path,
                sourcePath: null,
                isDirectory: true,
                size: 0,
                modifiedTime: new DateTimeOffset(directory.LastWriteTimeUtc, TimeSpan.Zero),
                attributes: (uint)directory.Attributes);
        }
        var file = new FileInfo(sourcePath);
        if (file.Exists is false) {
            throw new FileNotFoundException("The source file does not exist.", sourcePath);
        }
        return new SevenZipUpdateItem(
            path: path,
            sourcePath: sourcePath,
            isDirectory: false,
            size: file.Length,
            modifiedTime: new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
            attributes: (uint)file.Attributes);
    }

    private static int IndexOf(IReadOnlyList<SevenZipArchiveEntry> entries, string name) {
        /*
         * The name that a nesting holds is the name of the entry as it is
         * stored in its archive. Names that are spelled differently but
         * normalize to the same name also match.
         */
        for (var i = 0; i < entries.Count; i++) {
            var entry = entries[i];
            var pathIsEqual = string.Equals(entry.Path, name, StringComparison.Ordinal);
            if (pathIsEqual && entry.IsDirectory is false) {
                return i;
            }
        }
        var wanted = new SevenZipEntryName(name).Normalized;
        for (var i = 0; i < entries.Count; i++) {
            var entry = entries[i];
            if (entry.IsDirectory) {
                continue;
            }
            var entryName = new SevenZipEntryName(entry.Path).Normalized;
            var nameIsEqual = string.Equals(entryName, wanted, StringComparison.Ordinal);
            if (nameIsEqual) {
                return i;
            }
        }
        return -1;
    }

    private static (List<uint> Kept, List<SevenZipUpdateItem> Items) PlanUpdate(
        IReadOnlyList<SevenZipArchiveEntry> entries,
        IReadOnlyDictionary<string, string> added,
        ISet<string> delete) {
        /*
         * The items of an archive are rewritten when it is updated, so the
         * items that are kept have to be told to the library. The items that
         * are removed are left out, and the items that are added replace the
         * items that use the same name.
         */
        var deletePaths = delete.Select(NormalizeArchiveSeparators).ToArray();
        var removed = new HashSet<string>(deletePaths, StringComparer.Ordinal);
        foreach (var entry in entries) {
            var normalizedPath = NormalizeArchiveSeparators(entry.Path);
            foreach (var normalizedName in deletePaths) {
                if (normalizedPath.StartsWith(normalizedName + "/", StringComparison.Ordinal)) {
                    removed.Add(normalizedPath);
                }
            }
        }
        var kept = new List<uint>();
        for (var i = 0; i < entries.Count; i++) {
            var entry = entries[i];
            if (removed.Contains(NormalizeArchiveSeparators(entry.Path))) {
                continue;
            }
            var isReplaced = added.ContainsKey(entry.Path);
            if (isReplaced) {
                continue;
            }
            kept.Add((uint)i);
        }
        var items = added
            .Select(pair => CreateUpdateItem(pair.Key, pair.Value))
            .ToList();
        return (kept, items);
    }

    /*
     * Tests use this fallback when no composition scope exists. Runtime
     * callers resolve the archive identity asynchronously before opening it.
     */
    internal static SevenZipArchivePath GetForTest(FileInfo file) {
        ArgumentNullException.ThrowIfNull(file);
        return Get(SevenZipArchiveIdentity.CreateFallback(file), new SevenZipArchiveNest([]));
    }

    internal static SevenZipArchivePath Get(SevenZipArchiveIdentity identity) {
        return Get(identity, new SevenZipArchiveNest([]));
    }

    internal static SevenZipArchivePath Get(SevenZipArchiveIdentity identity, SevenZipArchiveNest nest) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(nest);
        var key = (Identity: identity, Nest: new NestKey(nest));
        lock (SetLocker) {
            if (Set.TryGetValue(key, out var value) is false) {
                Set[key] = value = new SevenZipArchivePath(identity, nest);
            }
            return value.Referenced();
        }
    }

    public SevenZipArchiveFileEvent FileChanged => field ??= new();

    public SevenZipArchiveFileEvent FileDeleted => field ??= new();

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
                    ArchiveProviderIDFormat.SevenZip,
                    File.FullName,
                    nesting,
                    new SevenZipEntryName(lastMember).Parent);
                field = ArchiveProviderIDCodec.Format(parentLocation);
            }
            return field;
        }
    }

    public string FileName =>
        File.FullName;

    public FileInfo File { get; }

    public SevenZipArchiveNest Nest { get; }

    public static async Task<SevenZipArchivePath> GetAsync(FileInfo file, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(file);
        var identity = await SevenZipArchiveIdentity.Create(file, token);
        return Get(identity, new SevenZipArchiveNest([]));
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

    public SevenZipEntryInfoCollection EntryInfo() {
        return EntryInfoCache.Result;
    }

    public Task<SevenZipEntryInfoCollection> EntryInfo(CancellationToken token) {
        return EntryInfoCache.Ready(token);
    }

    public async Task Read(SevenZipArchiveRead info, IOperationProgress progress, CancellationToken token) {
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
                    var archive = stage.Innermost;
                    foreach (var name in extract.Keys) {
                        if (token.IsCancellationRequested) {
                            token.ThrowIfCancellationRequested();
                        }
                        if (progress is not null) {
                            progress.Change(data: name);
                        }
                        var index = archive.IndexOf(name);
                        if (index >= 0) {
                            if (Log.Info()) {
                                Log.Info(nameof(archive.Extract) + " > " + name);
                            }
                            await RunArchiveOperation(
                                name,
                                progress,
                                (childProgress, childToken) => {
                                    archive.Extract(
                                        index,
                                        extract[name],
                                        info.ExtractOverwrites,
                                        childProgress,
                                        childToken);
                                    return Task.CompletedTask;
                                },
                                token);
                        }
                    }
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
                async (childProgress, childToken) => {
                    using (var stage = await OpenInnermost(childProgress, childToken)) {
                        stage.Innermost.ExtractAll(directory, overwrite, childProgress, childToken);
                    }
                },
                token);
        }
    }

    public async Task Update(SevenZipArchiveUpdate info, IOperationProgress progress, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(info);
        if (Log.Info()) {
            Log.Info(nameof(Update) + " > " + FullName);
        }
        var delete = new HashSet<string>(info.DeleteEntries.Where(i => i is not null));
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
        if (streamIsEmpty && createIsEmpty && deleteIsEmpty) {
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
                            "SevenZip",
                            Guid.NewGuid().ToString("N"));
                        while (Directory.Exists(directory)) {
                            directory = Path.Combine(
                                Path.GetTempPath(),
                                "Brows",
                                "SevenZip",
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
                    await WriteOutermost(added, delete, info.CompressionLevel, operationName, progress, token);
                }
                else {
                    await WriteNested(added, delete, info.CompressionLevel, operationName, progress, token);
                }
                if (progress is not null) {
                    foreach (var name in delete) {
                        progress.Change(data: name);
                    }
                    foreach (var name in create.Keys) {
                        progress.Change(data: name);
                    }
                }
            }
        }
        finally {
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
    }

    private async Task WriteOutermost(IReadOnlyDictionary<string, string> added,
                                      ISet<string> delete,
                                      int? compressionLevel,
                                      string operationName,
                                      IOperationProgress progress,
                                      CancellationToken token) {
        File.Refresh();
        var existing = new List<SevenZipArchiveEntry>();
        var archiveExists = File.Exists;
        if (archiveExists) {
            using (var archive = SevenZipArchive.Open(File.FullName)) {
                existing.AddRange(archive.Entries);
            }
        }
        var (kept, items) = PlanUpdate(existing, added, delete);
        if (archiveExists) {
            if (Log.Info()) {
                Log.Info(nameof(SevenZipArchive.Update) + " > " + FullName);
            }
            await RunArchiveOperation(
                operationName,
                progress,
                (childProgress, childToken) => {
                    SevenZipArchive.Update(
                        File.FullName,
                        kept,
                        items,
                        compressionLevel,
                        childProgress,
                        childToken);
                    return Task.CompletedTask;
                },
                token);
            return;
        }
        if (Log.Info()) {
            Log.Info(nameof(SevenZipArchive.Create) + " > " + FullName);
        }
        await RunArchiveOperation(
            operationName,
            progress,
            (childProgress, childToken) => {
                SevenZipArchive.Create(
                    File.FullName,
                    items,
                    compressionLevel,
                    childProgress,
                    childToken);
                return Task.CompletedTask;
            },
            token);
    }

    private async Task WriteNested(IReadOnlyDictionary<string, string> added,
                                   ISet<string> delete,
                                   int? compressionLevel,
                                   string operationName,
                                   IOperationProgress progress,
                                   CancellationToken token) {
        if (Log.Info()) {
            Log.Info(nameof(SevenZipArchive.Update) + " > " + FullName);
        }
        using (var stage = await OpenInnermost(progress, token)) {
            var entriesByLevel = new List<IReadOnlyList<SevenZipArchiveEntry>>();
            var filesByLevel = new List<string>(stage.Files);
            var nestNames = Nest.ToList();
            foreach (var archive in stage.Archives) {
                entriesByLevel.Add(archive.Entries);
            }
            var (kept, items) = PlanUpdate(entriesByLevel[^1], added, delete);
            stage.Close();
            var updated = filesByLevel[^1];
            await RunArchiveOperation(
                operationName,
                progress,
                (childProgress, childToken) => {
                    SevenZipArchive.Update(
                        filesByLevel[^1],
                        kept,
                        items,
                        compressionLevel,
                        childProgress,
                        childToken);
                    return Task.CompletedTask;
                },
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
                var keptIndices = new List<uint>();
                for (var i = 0; i < entries.Count; i++) {
                    if (i == index) {
                        continue;
                    }
                    keptIndices.Add((uint)i);
                }
                var replacement = new[] { CreateUpdateItem(nestedName, updated) };
                await RunArchiveOperation(
                    operationName,
                    progress,
                    (childProgress, childToken) => {
                        SevenZipArchive.Update(
                            filesByLevel[level],
                            keptIndices,
                            replacement,
                            compressionLevel,
                            childProgress,
                            childToken);
                        return Task.CompletedTask;
                    },
                    token);
                updated = filesByLevel[level];
            }
        }
    }

    private async Task<Stage> OpenInnermost(IOperationProgress progress, CancellationToken token) {
        if (Log.Info()) {
            Log.Info(nameof(OpenInnermost) + " > " + FullName);
        }
        var stage = new Stage(File.FullName);
        try {
            var archive = SevenZipArchive.Open(File.FullName);
            stage.Push(archive);
            foreach (var nested in Nest) {
                token.ThrowIfCancellationRequested();
                var index = IndexOf(archive.Entries, nested);
                var nestedNotFound = index < 0;
                if (nestedNotFound) {
                    throw new FileNotFoundException("The nested archive was not found.", nested);
                }
                var staged = stage.CreateFile();
                if (progress is null) {
                    archive.Extract(index, staged, overwrite: true, progress, token);
                }
                else {
                    await RunArchiveOperation(nested, progress, (childProgress, childToken) => {
                        archive.Extract(index, staged, overwrite: true, childProgress, childToken);
                        return Task.CompletedTask;
                    }, token);
                }
                archive = SevenZipArchive.Open(staged);
                stage.Push(archive);
            }
            return stage;
        }
        catch {
            stage.Dispose();
            throw;
        }
    }

    private sealed class Stage : IDisposable {
        private readonly List<SevenZipArchive> ArchiveList = [];
        private readonly List<string> FileList = [];
        private readonly List<string> DirectoryList = [];

        public Stage(string initialFile) {
            FileList.Add(initialFile ?? throw new ArgumentNullException(nameof(initialFile)));
        }

        public IReadOnlyList<SevenZipArchive> Archives =>
            ArchiveList;

        public IReadOnlyList<string> Files =>
            FileList;

        public SevenZipArchive Innermost =>
            ArchiveList[^1];

        public void Push(SevenZipArchive archive) {
            ArchiveList.Add(archive ?? throw new ArgumentNullException(nameof(archive)));
        }

        public string CreateFile() {
            for (; ; ) {
                var directory = Path.Combine(
                    Path.GetTempPath(),
                    "Brows",
                    "SevenZip",
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

        public void Close() {
            for (var i = ArchiveList.Count - 1; i >= 0; i--) {
                try {
                    ArchiveList[i].Dispose();
                }
                catch (Exception ex) {
                    if (Log.Error()) {
                        Log.Error(ex);
                    }
                }
            }
            ArchiveList.Clear();
        }

        public void Dispose() {
            Close();
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

    public async Task<bool> Drop(IPanelDrop data, IOperationProgress progress, CancellationToken token) {
        if (data is null) {
            return false;
        }
        var copy = data.CopyFiles ?? Array.Empty<string>();
        var move = data.MoveFiles ?? Array.Empty<string>();
        var files = new HashSet<string>(copy.Concat(move));
        if (files.Count == 0) {
            return false;
        }
        var update = Update(token: token, progress: progress, info: new() {
            CreateEntriesFromFiles = files.ToDictionary(
                file => Path.GetFileName(file),
                file => file)
        });
        await update;
        return true;
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

        protected SevenZipEntryInfo EntryInfo { get; }

        protected StreamSource(TEntry entry, SevenZipEntryInfo entryInfo) : base(entry) {
            EntryInfo = entryInfo ?? throw new ArgumentNullException(nameof(entryInfo));
        }

        protected sealed override async Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            if (EntryInfo.Kind == SevenZipEntryKind.File) {
                NormalizeArchiveRelativePath(EntryInfo.Name.Original);
                /*
                 * This adapter stages the entry in a temporary file and owns that file until
                 * the returned stream-ready lease is disposed.
                 */
                var directory = Path.Combine(
                    Path.GetTempPath(),
                    "Brows",
                    "SevenZip",
                    Guid.NewGuid().ToString("N"));
                while (Directory.Exists(directory)) {
                    directory = Path.Combine(
                        Path.GetTempPath(),
                        "Brows",
                        "SevenZip",
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
}
