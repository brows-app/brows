using Brows.IO.Compression;
using Brows.Shared.Archives;
using Brows.Zip;
using Domore.IO;
using Domore.Logs;
using Domore.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

public sealed class ZipArchivePath {
    private static readonly ILog Log = Logging.For(typeof(ZipArchivePath));
    private static readonly Dictionary<string, ZipArchivePath> Set = new();

    private int ReferenceCount;

    private readonly ArchiveLocker Locker;
    private readonly IDisposable FileEvent;
    private readonly Lock EntryInfoCacheLocker = new();

    private TaskCache<ZipEntryInfoCollection> EntryInfoCache {
        get {
            lock (EntryInfoCacheLocker) {
                return field ??= new(EntryInfoLoad);
            }
        }
        set {
            lock (EntryInfoCacheLocker) {
                field = value;
            }
        }
    }

    private ZipArchivePath(FileInfo file, ZipArchiveNest nest, string fullName) {
        Nest = nest ?? throw new ArgumentNullException(nameof(nest));
        File = file ?? throw new ArgumentNullException(nameof(file));
        FileEvent = FileSystemEventTasks.Add(File.DirectoryName, FileEventTask);
        Locker = ArchiveLocker.Get(file);
        FullName = fullName;
    }

    private async Task<ZipEntryInfoCollection> EntryInfoLoad(CancellationToken token) {
        if (Log.Info()) {
            Log.Info(nameof(Read) + " > " + FullName);
        }
        var locked = Locker.Lock(token);
        using (await locked) {
            ZipEntryInfoCollection work() {
                using (var archive = Archive.Open(this, ZipArchiveMode.Read, token)) {
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    var fileSet = new Dictionary<string, ZipEntryInfo>();
                    var pathSet = new Dictionary<string, ZipEntryInfo>();
                    var entries = archive.Zip.Entries;
                    foreach (var entry in entries) {
                        if (token.IsCancellationRequested) {
                            token.ThrowIfCancellationRequested();
                        }
                        if (entry != null) {
                            if (Log.Info()) {
                                Log.Info(nameof(entry) + " > " + entry.FullName);
                            }
                            var key = new ZipEntryName(entry.FullName);
                            var dir = entry.IsDirectory();
                            if (dir) {
                                pathSet.TryAdd(key.Normalized, ZipEntryInfo.Path(this, key));
                            }
                            else {
                                /*
                                 * Name lookup reads the first member. Keep its metadata and original
                                 * spelling when duplicate or separator-equivalent names are present.
                                 */
                                fileSet.TryAdd(key.Normalized, ZipEntryInfo.File(this, key, entry));
                            }
                        }
                    }
                    var namedEntries = fileSet.Values.Concat(pathSet.Values).ToArray();
                    foreach (var item in namedEntries) {
                        if (token.IsCancellationRequested) {
                            token.ThrowIfCancellationRequested();
                        }
                        foreach (var path in item.Name.Paths) {
                            if (token.IsCancellationRequested) {
                                token.ThrowIfCancellationRequested();
                            }
                            if (pathSet.TryGetValue(path, out _) == false) {
                                pathSet[path] = ZipEntryInfo.Path(this, new ZipEntryName(path));
                            }
                        }
                    }
                    return new ZipEntryInfoCollection(
                        items: pathSet.Values.Concat(fileSet.Values));
                }
            }
            return await Task.Run(work, token);
        }
    }

    private ZipArchivePath Referenced() {
        if (Log.Info()) {
            Log.Info(nameof(Referenced) + " > " + FullName + " > " + ReferenceCount);
        }
        ReferenceCount++;
        return this;
    }

    private sealed class Reference : IDisposable {
        private ZipArchivePath Path;

        public Reference(ZipArchivePath path) {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public void Dispose() {
            var path = Interlocked.Exchange(ref Path, null);
            if (path is not null) {
                path.Release();
                GC.SuppressFinalize(this);
            }
        }

        ~Reference() {
            try {
                Dispose();
            }
            catch (Exception ex) {
                LogCleanupException(ex);
            }
        }
    }

    private Reference Retain() {
        lock (Set) {
            var isCurrent = Set.TryGetValue(FullName, out var current) && ReferenceEquals(current, this);
            var isUnreferenced = ReferenceCount <= 0;
            var cannotRetain = isCurrent == false || isUnreferenced;
            if (cannotRetain) {
                throw new ObjectDisposedException(nameof(ZipArchivePath));
            }
            Referenced();
            return new Reference(this);
        }
    }

    private static string MakeFullName(FileInfo file, ZipArchiveNest nest) {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(nest);
        var archiveRoot = new ArchiveProviderID(
            ArchiveProviderIDFormat.Zip,
            file.FullName,
            nest,
            "");
        return ArchiveProviderIDCodec.Format(archiveRoot);
    }

    private static void LogCleanupException(Exception ex) {
        try {
            if (Log.Error()) {
                Log.Error(ex);
            }
        }
        catch {
        }
    }

    private static string NormalizeArchiveEntryName(string name, bool directory) {
        ArgumentNullException.ThrowIfNull(name);
        var normalized = name
            .Replace('\\', '/')
            .Trim('/');
        if (normalized.Length == 0) {
            throw new InvalidDataException("A ZIP entry must have a non-empty relative name.");
        }
        var needsDirectorySeparator = directory && normalized.EndsWith('/') == false;
        if (needsDirectorySeparator) {
            normalized += "/";
        }
        return normalized;
    }

    private static string NormalizeArchiveDirectory(string directory) {
        if (string.IsNullOrEmpty(directory)) {
            return null;
        }
        return directory
            .Replace('\\', '/')
            .Trim('/');
    }

    private static string CombineArchiveEntryName(string directory, string name) {
        if (string.IsNullOrEmpty(directory)) {
            return name;
        }
        return directory + "/" + name;
    }

    private void InvalidateRelatedEntryInfo() {
        /*
         * A commit replaces the physical archive, so every cached view of that archive must be discarded.
         */
        lock (Set) {
            foreach (var path in Set.Values) {
                var sharesArchive = ReferenceEquals(path.Locker, Locker);
                if (sharesArchive) {
                    path.EntryInfoCache = null;
                }
            }
        }
    }

    private async Task FileEventTask(FileSystemEventArgs e, CancellationToken token) {
        if (e is null) {
            return;
        }
        var names = e is RenamedEventArgs renamed
            ? new[] { e.Name, renamed.OldName }
            : new[] { e.Name };
        var concernsArchive = names.Contains(File.Name, StringComparer.OrdinalIgnoreCase);
        if (concernsArchive == false) {
            return;
        }
        var update = await FileSystemTask.ExistingFile(FileName, token);
        InvalidateEntryInfo();
        if (update is null) {
            await FileDeleted.All(token);
            return;
        }
        await FileChanged.All(token);
    }

    internal void InvalidateEntryInfo() {
        /*
         * Each path has its own watcher subscription and refresh callback. Invalidating another path here could
         * discard metadata that its callback has already reloaded for the same filesystem event.
         */
        EntryInfoCache = null;
    }

    internal async Task<bool> Drop(
        IPanelDrop data,
        string targetDirectory,
        IOperationProgress progress,
        CancellationToken token) {
        if (data == null) {
            return false;
        }
        var copy = data.CopyFiles ?? Array.Empty<string>();
        var move = data.MoveFiles ?? Array.Empty<string>();
        var files = new HashSet<string>(copy.Concat(move));
        if (files.Count == 0) {
            return false;
        }
        var directory = NormalizeArchiveDirectory(targetDirectory);
        var update = Update(token: token, progress: progress, info: new() {
            CreateEntriesFromFiles = files.ToDictionary(
                file => CombineArchiveEntryName(
                    directory,
                    NormalizeArchiveEntryName(Path.GetFileName(file), directory: false)),
                file => file)
        });
        await update;
        return true;
    }

    public ZipArchiveFileEvent FileChanged => _FileChanged ??= new();
    private ZipArchiveFileEvent _FileChanged;

    public ZipArchiveFileEvent FileDeleted => _FileDeleted ??= new();
    private ZipArchiveFileEvent _FileDeleted;

    public string Parent {
        get {
            if (_Parent is null) {
                if (Nest.Count == 0) {
                    _Parent = File.DirectoryName;
                }
                else {
                    var parentNest = Nest.Take(Nest.Count - 1).ToArray();
                    var nestedArchiveName = Nest.Last();
                    var parentDirectory = new ZipEntryName(nestedArchiveName).Parent;
                    var parentLocation = new ArchiveProviderID(
                        ArchiveProviderIDFormat.Zip,
                        File.FullName,
                        parentNest,
                        parentDirectory);
                    _Parent = ArchiveProviderIDCodec.Format(parentLocation);
                }
            }
            return _Parent;
        }
    }
    private string _Parent;

    public string FullName { get; }
    public string FileName =>
        File.FullName;

    public FileInfo File { get; }
    public ZipArchiveNest Nest { get; }

    public static ZipArchivePath Get(FileInfo file, ZipArchiveNest nest) {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(nest);
        var key = MakeFullName(file, nest);
        lock (Set) {
            if (Set.TryGetValue(key, out var value) == false) {
                Set[key] = value = new ZipArchivePath(file, nest, key);
            }
            return value.Referenced();
        }
    }

    public void Release() {
        lock (Set) {
            if (Log.Info()) {
                Log.Info(nameof(Release) + " > " + FullName);
            }
            var count = --ReferenceCount;
            if (count == 0) {
                if (Log.Info()) {
                    Log.Info(nameof(Set.Remove));
                }
                Set.Remove(FullName);
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

    public ZipEntryInfoCollection EntryInfo() {
        return EntryInfoCache.Result;
    }

    public Task<ZipEntryInfoCollection> EntryInfo(CancellationToken token) {
        return EntryInfoCache.Ready(token);
    }

    public async Task Read(ZipArchiveRead info, IOperationProgress progress, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(info);
        progress?.Change(kind: OperationProgressKind.FileSize);
        if (Log.Info()) {
            Log.Info(nameof(Read) + " > " + FullName);
        }
        var extract = new Dictionary<string, string>(info.ExtractEntriesToFiles);
        if (extract.Count == 0) {
            return;
        }
        var locked = Locker.Lock(token);
        using (await locked) {
            async Task WorkAsync() {
                using (var archive = Archive.Open(this, ZipArchiveMode.Read, progress, token)) {
                    /*
                     * Resolve the entire request before publishing output, including when a later name is stale.
                     */
                    var entries = new List<(ZipArchiveEntry Entry, string Destination)>(extract.Count);
                    foreach (var extraction in extract) {
                        token.ThrowIfCancellationRequested();
                        var entry = archive.Zip.GetEntry(extraction.Key);
                        if (entry is null) {
                            throw new FileNotFoundException(
                                $"The ZIP entry '{extraction.Key}' was not found in archive '{FullName}'.",
                                extraction.Key);
                        }
                        entries.Add((entry, extraction.Value));
                    }
                    foreach (var (entry, destination) in entries) {
                        token.ThrowIfCancellationRequested();
                        if (progress != null) {
                            progress.Change(data: entry.FullName);
                        }
                        if (Log.Info()) {
                            Log.Info(nameof(ZipEntryExtraction.ExtractAsync) + " > " + entry.FullName);
                        }
                        await ZipEntryExtraction.ExtractAsync(
                            entry,
                            destination,
                            info.ExtractOverwrites,
                            progress,
                            token);
                    }
                }
            }
            await Task.Run(WorkAsync, token);
        }
    }

    public async Task Update(ZipArchiveUpdate info, IOperationProgress progress, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(info);
        if (Log.Info()) {
            Log.Info(nameof(Update) + " > " + FullName);
        }
        var delete = new HashSet<string>(info.DeleteEntries.Where(i => i != null), StringComparer.Ordinal);
        var additions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in info.CreateEntriesFromFiles) {
            var name = NormalizeArchiveEntryName(entry.Key, entry.Value is null);
            additions[name] = entry.Value;
        }
        var stream = new List<IEntryStreamSet>(info.CopyStreamSets.Where(i => i != null));
        var hasNoUpdates = stream.Count == 0 && additions.Count == 0 && delete.Count == 0;
        if (hasNoUpdates) {
            return;
        }
        progress?.Change(kind: OperationProgressKind.FileSize);
        using var staging = new ArchiveUpdateStaging();
        var targetDirectory = NormalizeArchiveDirectory(info.CopyStreamTargetDirectory);
        foreach (var set in stream) {
            await set.Consume(
                consuming: async (source, sourceStream, progress, token) => {
                    token.ThrowIfCancellationRequested();
                    var isDirectory = sourceStream is null && source.SourceDirectory is not null;
                    var name = NormalizeArchiveEntryName(source.RelativePath, isDirectory);
                    name = CombineArchiveEntryName(targetDirectory, name);
                    if (sourceStream is null) {
                        if (isDirectory) {
                            additions[name] = null;
                        }
                        return;
                    }

                    var stagedSource = staging.CreateSourceFile();
                    var stagedSourceFileName = stagedSource.Name;
                    await using (stagedSource) {
                        var streamLength = source.StreamLength;
                        var streamLengthIsKnown = streamLength > 0;
                        if (progress is not null && streamLengthIsKnown) {
                            progress.Change(addTarget: streamLength, data: name);
                        }
                        await ZipArchiveUpdateRewriter.CopyBytesAsync(
                            sourceStream,
                            stagedSource,
                            progress,
                            streamLengthIsKnown == false,
                            token);
                        await stagedSource.FlushAsync(token);
                    }
                    additions[name] = stagedSourceFileName;
                },
                progress: progress,
                token: token);
        }

        var locked = Locker.Lock(token);
        using (await locked) {
            token.ThrowIfCancellationRequested();
            var removedNames = new HashSet<string>(delete, StringComparer.Ordinal);
            foreach (var name in additions.Keys) {
                removedNames.Add(name);
            }
            var rewriter = new ZipArchiveUpdateRewriter(staging, progress);
            /* Keep external writers from changing the source until the staged replacement is ready. */
            await using var originalArchive = new FileStream(
                FileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await rewriter.RewriteAsync(FileName, Nest, additions, removedNames, token);
            await originalArchive.DisposeAsync();
            token.ThrowIfCancellationRequested();
            staging.CommitArchive(FileName);
            InvalidateRelatedEntryInfo();
        }
    }

    public Task<bool> Drop(IPanelDrop data, IOperationProgress progress, CancellationToken token) {
        return Drop(data, null, progress, token);
    }

    public abstract class StreamSource<TEntry> : EntryStreamSource<TEntry> where TEntry : IEntry {
        private Archive Archive;
        private ArchiveLocked Locked;
        private IDisposable ArchivePathReference;

        private sealed class EntryNotFoundException : Exception {
        }

        private sealed class Disposable : IEntryStreamReady {
            private readonly Reference PathReference;
            private int Disposed;

            private void DisposeResources() {
                var alreadyDisposed = Interlocked.Exchange(ref Disposed, 1) != 0;
                if (alreadyDisposed) {
                    return;
                }
                var archive = Source.Archive;
                Source.Archive = null;
                var locked = Source.Locked;
                Source.Locked = null;
                Source.Stream = null;
                try {
                    archive?.Dispose();
                }
                finally {
                    try {
                        locked?.Dispose();
                    }
                    finally {
                        try {
                            PathReference?.Dispose();
                        }
                        finally {
                            Source.ReleaseArchivePathReference();
                        }
                    }
                }
            }

            public StreamSource<TEntry> Source { get; }

            public Disposable(StreamSource<TEntry> source, Reference pathReference) {
                Source = source ?? throw new ArgumentNullException(nameof(source));
                PathReference = pathReference;
            }

            void IDisposable.Dispose() {
                try {
                    DisposeResources();
                }
                finally {
                    GC.SuppressFinalize(this);
                }
            }

            ~Disposable() {
                try {
                    DisposeResources();
                }
                catch (Exception ex) {
                    LogCleanupException(ex);
                }
            }
        }

        internal void ReleaseArchivePathReference() {
            Interlocked.Exchange(ref ArchivePathReference, null)?.Dispose();
        }

        protected ZipEntryInfo EntryInfo { get; }

        protected StreamSource(TEntry entry, ZipEntryInfo entryInfo) : base(entry) {
            EntryInfo = entryInfo ?? throw new ArgumentNullException(nameof(entryInfo));
            if (EntryInfo.Kind == ZipEntryKind.File) {
                ArchivePathReference = EntryInfo.Archive.Retain();
            }
        }

        protected sealed override async Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            ZipArchivePath openedPath = null;
            Reference openedPathReference = null;
            ArchiveLocked locked = null;
            Archive archive = null;
            try {
                if (EntryInfo.Kind == ZipEntryKind.File) {
                    openedPath = Get(EntryInfo.Archive.File, EntryInfo.Archive.Nest);
                    openedPathReference = new Reference(openedPath);
                    locked = await openedPath.Locker.Lock(token);
                    archive = await Task.Run(
                        () => Archive.Open(openedPath, ZipArchiveMode.Read, token),
                        token);
                    token.ThrowIfCancellationRequested();
                    var entry = archive.Zip.GetEntry(EntryInfo.Name.Original);
                    if (entry is null) {
                        throw new EntryNotFoundException();
                    }
                    token.ThrowIfCancellationRequested();
                    var entryStream = entry.Open();
                    ZipEntryValidationStream validationStream;
                    try {
                        token.ThrowIfCancellationRequested();
                        validationStream = new ZipEntryValidationStream(
                            entryStream,
                            entry.Length,
                            unchecked((uint)entry.Crc32));
                        token.ThrowIfCancellationRequested();
                    }
                    catch {
                        entryStream.Dispose();
                        throw;
                    }
                    Archive = archive;
                    Locked = locked;
                    Stream = validationStream;
                    archive = null;
                    locked = null;
                }
                var ready = new Disposable(this, openedPathReference);
                openedPathReference = null;
                openedPath = null;
                return ready;
            }
            catch {
                try {
                    archive?.Dispose();
                }
                finally {
                    try {
                        locked?.Dispose();
                    }
                    finally {
                        try {
                            if (openedPathReference is not null) {
                                openedPathReference.Dispose();
                            }
                            else {
                                openedPath?.Release();
                            }
                        }
                        finally {
                            ReleaseArchivePathReference();
                        }
                    }
                }
                throw;
            }
        }

        public sealed override long StreamLength =>
            EntryInfo.SizeOriginal ?? 0;
    }
}
