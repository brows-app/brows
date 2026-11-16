using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

public abstract class FileProtocolProvider<TEntry, TConfig> : Provider<TEntry, TConfig> where TEntry : FileProtocolEntry where TConfig : FileProtocolConfig, new() {
    private static readonly ILog Log = Logging.For(typeof(FileProtocolProvider<TEntry, TConfig>));

    private readonly SemaphoreSlim ListLocker = new(1, 1);
    private CancellationTokenSource ListTokenSource;

    private async Task PrivateList(CancellationToken token) {
        try {
            ListTokenSource?.Cancel();
        }
        catch (ObjectDisposedException) {
        }
        try {
            await ListLocker.WaitAsync(token);
        }
        catch (ObjectDisposedException) {
            return;
        }
        try {
            using var ts = ListTokenSource = new();
            using var lt = CancellationTokenSource.CreateLinkedTokenSource(token, ts.Token);
            token = lt.Token;
            try {
                await Revoke(Provided, token);
                await List(token);
            }
            catch (OperationCanceledException) when (ts.IsCancellationRequested) {
            }
        }
        finally {
            try {
                ListLocker.Release();
            }
            catch (ObjectDisposedException) {
            }
        }
    }

    protected sealed override async Task Begin(CancellationToken token) {
        await PrivateList(token);
    }

    protected sealed override async Task Refresh(CancellationToken token) {
        await PrivateList(token);
    }

    protected FileProtocolProvider(Uri uri, int initialCapacity, IEqualityComparer<string> compareID, IEqualityComparer<string> compareName) : base(uri?.ToString(), initialCapacity, compareID, compareName) {
        Uri = uri ?? throw new ArgumentNullException(nameof(uri));
    }

    protected abstract Task List(CancellationToken token);

    protected override void Dispose(bool disposing) {
        if (disposing) {
            ListLocker.Dispose();
        }
        base.Dispose(disposing);
    }

    public Uri Uri { get; }

    public sealed override string Parent {
        get {
            if (_Parent == null) {
                _Parent = FileProtocolUri.Parent(Uri)?.ToString() ?? "";
            }
            return _Parent == ""
                ? null
                : _Parent;
        }
    }
    private string _Parent;

    protected abstract class ProvideIO<TProvider> : IProvideIO, IProviderExport<TProvider> where TProvider : FileProtocolProvider<TEntry, TConfig> {
        protected abstract FileProtocolStreamSource<TEntry> Read(TProvider provider, TEntry entry);
        protected abstract IAsyncEnumerable<TEntry> List(TProvider provider, Uri uri, CancellationToken token);

        private sealed class DirectoryStreamSource : FileProtocolStreamSource<TEntry> {
            public sealed override string RelativePath { get; }
            public sealed override string SourceDirectory => RelativePath;
            public sealed override long StreamLength => 0;

            public DirectoryStreamSource(Uri providerUri, TEntry entry) : base(entry) {
                RelativePath = FileProtocolUri.RelativePath(providerUri, entry.Uri);
            }
        }

        async Task<bool> IProvideIO.Work(ICollection<IProvidedIO> io, ICommandSource source, IProvider target, IOperationProgress progress, CancellationToken token) {
            if (io is null) throw new ArgumentNullException(nameof(io));
            if (source is null) return false;
            if (target is not TProvider provider) {
                return false;
            }
            var itemEntryList = source.Items?.OfType<TEntry>()?.ToList();
            if (itemEntryList == null || itemEntryList.Count == 0) {
                return false;
            }
            async IAsyncEnumerable<TEntry> itemEntryListAsync([EnumeratorCancellation] CancellationToken token) {
                foreach (var entry in itemEntryList) {
                    yield return entry;
                }
                await Task.CompletedTask;
            }
            async IAsyncEnumerable<FileProtocolStreamSource<TEntry>> streamFilesAndAddDirectories(IAsyncEnumerable<TEntry> entries, List<TEntry> directories, [EnumeratorCancellation] CancellationToken token) {
                ArgumentNullException.ThrowIfNull(entries);
                ArgumentNullException.ThrowIfNull(directories);
                await foreach (var entry in entries.WithCancellation(token)) {
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    if (entry is null) {
                        continue;
                    }
                    if (entry.Kind == FileProtocolEntryKind.File) {
                        yield return Read(provider, entry);
                    }
                    if (entry.Kind == FileProtocolEntryKind.Directory) {
                        directories.Add(entry);
                        yield return new DirectoryStreamSource(provider.Uri, entry);
                    }
                    var entryIsLink = entry.Kind is
                        FileProtocolEntryKind.FileLink or FileProtocolEntryKind.DirectoryLink;
                    if (entryIsLink) {
                        if (Log.Warn()) {
                            Log.Warn(Log.Join(
                                "Copy rejected link entry",
                                entry.Kind,
                                FileProtocolUri.Redact(entry.Uri)));
                        }
                        throw new NotSupportedException($"Copying {entry.Kind} entries is not supported.");
                    }
                }
            }
            async IAsyncEnumerable<FileProtocolStreamSource<TEntry>> recurse([EnumeratorCancellation] CancellationToken token) {
                var dirs = new List<TEntry>();
                var topLevelEntries = itemEntryListAsync(token);
                var topLevelStreams = streamFilesAndAddDirectories(topLevelEntries, dirs, token);
                await foreach (var streamSource in topLevelStreams) {
                    yield return streamSource;
                }
                for (; ; ) {
                    var nextLevelDirs = dirs.ToList();
                    if (nextLevelDirs.Count == 0) {
                        break;
                    }
                    foreach (var dir in nextLevelDirs) {
                        if (Log.Info()) {
                            Log.Info(Log.Join(
                                "Copy listing source directory",
                                FileProtocolUri.RelativePath(provider.Uri, dir.Uri)));
                        }
                        var dirEntries = List(provider, dir.Uri, token);
                        var dirStreams = streamFilesAndAddDirectories(dirEntries, dirs, token);
                        await foreach (var streamSource in dirStreams) {
                            yield return streamSource;
                        }
                        dirs.Remove(dir);
                    }
                }
            }
            await Task.CompletedTask;
            var concurrency = provider.Config?.StreamSourceConcurrency ?? FileProtocolConfig.StreamSourceConcurrencyDefault;
            if (Log.Info()) {
                Log.Info(Log.Join(
                    "Copy providing source entries",
                    FileProtocolUri.Redact(provider.Uri),
                    itemEntryList.Count,
                    "concurrency",
                    concurrency));
            }
            var streamSet = new FileProtocolStreamSet<TEntry>(recurse(token), concurrency);
            io.Add(new ProvidedIO { StreamSets = new[] { streamSet } });
            return true;
        }
    }

    protected abstract class CopyProvidedIO<TProvider> : ICopyProvidedIO, IProviderExport<TProvider> where TProvider : FileProtocolProvider<TEntry, TConfig> {
        private sealed class PreparedStreamSet : IEntryStreamSet, IDisposable {
            private IEntryStreamReady Ready;

            private PreparedStreamSet(IEntryStreamReady ready, IReadOnlyList<IEntryStreamSource> sources) {
                Ready = ready;
                Sources = sources ?? throw new ArgumentNullException(nameof(sources));
            }

            private async IAsyncEnumerable<IEntryStreamSource> Enumerate(
                [EnumeratorCancellation] CancellationToken token) {
                foreach (var source in Sources) {
                    token.ThrowIfCancellationRequested();
                    yield return source;
                }
                await Task.CompletedTask;
            }

            internal IReadOnlyList<IEntryStreamSource> Sources { get; }

            internal static async Task<PreparedStreamSet> Create(IEntryStreamSet source, CancellationToken token) {
                ArgumentNullException.ThrowIfNull(source);
                var ready = source.StreamSourceReady();
                try {
                    var sources = new List<IEntryStreamSource>();
                    var streams = source.StreamSource(token);
                    if (streams is not null) {
                        await foreach (var stream in streams.WithCancellation(token)) {
                            token.ThrowIfCancellationRequested();
                            if (stream is not null) {
                                sources.Add(stream);
                            }
                        }
                    }
                    return new PreparedStreamSet(ready, sources);
                }
                catch {
                    ready?.Dispose();
                    throw;
                }
            }

            public IEntryStreamReady StreamSourceReady() {
                var ready = Ready;
                Ready = null;
                return ready;
            }

            public IEnumerable<string> FileSource() {
                return Array.Empty<string>();
            }

            public IAsyncEnumerable<IEntryStreamSource> StreamSource(CancellationToken token) {
                return Enumerate(token);
            }

            public void Dispose() {
                Ready?.Dispose();
                Ready = null;
            }
        }

        private sealed class FileItem {
            public string RelativePath => _RelativePath ??=
                Provided.RelativePath?.TrimEnd('/') ?? "";
            private string _RelativePath;

            public string DestinationPath => _DestinationPath ??=
                FileProtocolUri.CombinePath(Provider.Uri, RelativePath);
            private string _DestinationPath;

            public string OriginalPath =>
                Provided.OriginalPath;

            public ProvidedFile Provided { get; }
            public TProvider Provider { get; }

            public FileItem(TProvider provider, ProvidedFile provided) {
                Provided = provided ?? throw new ArgumentNullException(nameof(provided));
                Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            }
        }

        private readonly record struct CopyResult(bool Copied, bool Succeeded);

        private static IEnumerable<string> ParentDirectories(TProvider provider, string relativePath) {
            var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (var partCount = 1; partCount < parts.Length; partCount++) {
                var path = string.Join('/', parts.Take(partCount));
                yield return FileProtocolUri.CombinePath(provider.Uri, path);
            }
        }

        private async Task CreateParentDirectories(TProvider provider,
                                                   string relativePath,
                                                   HashSet<string> createdDirectories,
                                                   ProviderPostponedRefresh refresh,
                                                   CancellationToken token) {
            _ = FileProtocolUri.CombinePath(provider.Uri, relativePath);
            foreach (var path in ParentDirectories(provider, relativePath)) {
                if (createdDirectories.Add(path)) {
                    await EnsureDirectory(provider, path, token);
                    await refresh.Work(token);
                }
            }
        }

        private static async Task Copy(Stream source,
                                       Stream destination,
                                       long length,
                                       IOperationProgress progress,
                                       CancellationToken token) {
            var lengthIsKnown = length >= 0;
            progress?.Change(
                setProgress: 0,
                setTarget: Math.Max(0, length),
                kind: OperationProgressKind.FileSize);
            var buffer = new byte[81920];
            for (; ; ) {
                token.ThrowIfCancellationRequested();
                var read = await source.ReadAsync(buffer.AsMemory(), token);
                if (read == 0) {
                    break;
                }
                await destination.WriteAsync(buffer.AsMemory(0, read), token);
                progress?.Change(addProgress: read, addTarget: lengthIsKnown ? null : read);
            }
        }

        private static void LogUpload(string path, OperationResult result) {
            if (result.Kind == OperationResultKind.Failed) {
                if (Log.Warn()) {
                    Log.Warn(Log.Join("Upload failed", path, result.Error?.Message));
                }
                return;
            }
            if (result.Kind == OperationResultKind.Canceled) {
                if (Log.Info()) {
                    Log.Info(Log.Join("Upload canceled", path));
                }
            }
        }

        private async Task<CopyResult> CopyFromFiles(IReadOnlyCollection<ProvidedFile> files,
                                                     TProvider provider,
                                                     IOperationProgress progress,
                                                     CancellationToken token) {
            ArgumentNullException.ThrowIfNull(files);
            ArgumentNullException.ThrowIfNull(provider);
            if (files.Count == 0) {
                return new(false, true);
            }
            var items = files
                .Select(file => new FileItem(provider, file))
                .Where(item => !string.IsNullOrEmpty(item.RelativePath))
                .ToList();
            if (items.Count == 0) {
                return new(false, true);
            }

            var createdDirectories = new HashSet<string>(StringComparer.Ordinal);
            var refresh = new ProviderPostponedRefresh(provider);
            var copiedAnyFiles = false;
            var copiedAllFiles = true;
            foreach (var item in items) {
                await CreateParentDirectories(provider, item.RelativePath, createdDirectories, refresh, token);
            }
            foreach (var item in items) {
                token.ThrowIfCancellationRequested();
                var source = item.OriginalPath;
                async Task CopyFile(IOperationProgress childProgress, CancellationToken copyToken) {
                    var sourceFile = await FileSystemTask.ExistingFile(source, copyToken);
                    if (sourceFile is null) {
                        throw new FileNotFoundException("The source file no longer exists.", source);
                    }
                    var sourceLength = sourceFile.Length;
                    childProgress?.Change(
                        setProgress: 0,
                        setTarget: sourceLength,
                        kind: OperationProgressKind.FileSize);
                    using var destination = await Write(provider, item.DestinationPath, sourceLength, copyToken);
                    await using var fileSource = sourceFile.OpenRead();
                    await Copy(fileSource, destination, sourceLength, childProgress, copyToken);
                    if (Log.Info()) {
                        Log.Info(Log.Join("Uploaded", item.DestinationPath, sourceLength));
                    }
                }

                var fileCopied = true;
                if (progress is null) {
                    await CopyFile(null, token);
                }
                else {
                    var result = await progress.Child(
                        Path.GetFileName(source), CopyFile, OperationFailureBehavior.Record, token);
                    fileCopied = result.Kind == OperationResultKind.Succeeded;
                    LogUpload(item.DestinationPath, result);
                }
                token.ThrowIfCancellationRequested();
                if (fileCopied) {
                    copiedAnyFiles = true;
                }
                else {
                    copiedAllFiles = false;
                }
                await refresh.Work(token);
            }
            await refresh.Work(final: true, token);
            return new(copiedAnyFiles, copiedAllFiles);
        }

        private static async Task<List<PreparedStreamSet>> PrepareStreamSets(
            IReadOnlyList<IEntryStreamSet> entryStreamSets,
            CancellationToken token) {
            ArgumentNullException.ThrowIfNull(entryStreamSets);
            var preparedSets = new List<PreparedStreamSet>();
            try {
                foreach (var set in entryStreamSets) {
                    token.ThrowIfCancellationRequested();
                    preparedSets.Add(await PreparedStreamSet.Create(set, token));
                }
                token.ThrowIfCancellationRequested();
                return preparedSets;
            }
            catch {
                foreach (var set in preparedSets) {
                    set.Dispose();
                }
                throw;
            }
        }

        private async Task<CopyResult> CopyFromStreams(IReadOnlyList<PreparedStreamSet> preparedSets,
                                                       TProvider provider,
                                                       IOperationProgress progress,
                                                       CancellationToken token) {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(preparedSets);
            if (preparedSets.Count == 0) {
                return new(false, true);
            }

            var copiedAnySources = false;
            var copiedAllSources = true;
            var createdDirectories = new HashSet<string>(StringComparer.Ordinal);
            var refresh = new ProviderPostponedRefresh(provider);
            foreach (var set in preparedSets) {
                using var sourceSetReady = set.StreamSourceReady();
                foreach (var source in set.Sources) {
                    token.ThrowIfCancellationRequested();
                    progress?.Change(data: source.EntryName);
                    var sourceCopied = false;

                    async Task CopySource(IOperationProgress fileProgress, CancellationToken copyToken) {
                        using var sourceReady = await source.StreamReady(copyToken);
                        await using var stream = source.Stream;
                        if (stream is null) {
                            if (source.SourceDirectory is null) {
                                return;
                            }
                            var directoryRelativePath = source.RelativePath ?? "";
                            if (string.IsNullOrEmpty(directoryRelativePath)) {
                                return;
                            }
                            await CreateParentDirectories(
                                provider,
                                directoryRelativePath,
                                createdDirectories,
                                refresh,
                                copyToken);
                            var directoryPath = FileProtocolUri.CombinePath(provider.Uri, directoryRelativePath);
                            if (createdDirectories.Add(directoryPath)) {
                                copyToken.ThrowIfCancellationRequested();
                                await EnsureDirectory(provider, directoryPath, copyToken);
                                sourceCopied = true;
                                if (Log.Info()) {
                                    Log.Info(Log.Join("Uploaded directory", directoryPath));
                                }
                            }
                            return;
                        }

                        var relativePath = source.RelativePath ?? "";
                        if (string.IsNullOrEmpty(relativePath)) {
                            throw new InvalidDataException("The stream source has no relative path.");
                        }
                        var destinationPath = FileProtocolUri.CombinePath(provider.Uri, relativePath);
                        await CreateParentDirectories(
                            provider,
                            relativePath,
                            createdDirectories,
                            refresh,
                            copyToken);
                        fileProgress?.Change(
                            setProgress: 0,
                            setTarget: Math.Max(0, source.StreamLength),
                            kind: OperationProgressKind.FileSize);
                        await using (var destination = await Write(
                            provider,
                            destinationPath,
                            source.StreamLength,
                            copyToken)) {
                            await Copy(stream, destination, source.StreamLength, fileProgress, copyToken);
                        }
                        sourceCopied = true;
                        if (Log.Info()) {
                            Log.Info(Log.Join("Uploaded", destinationPath, source.StreamLength));
                        }
                    }

                    var sourceSucceeded = true;
                    if (progress is null) {
                        await CopySource(null, token);
                    }
                    else {
                        var result = await progress.Child(
                            source.EntryName, CopySource, OperationFailureBehavior.Record, token);
                        sourceSucceeded = result.Kind == OperationResultKind.Succeeded;
                        LogUpload(source.RelativePath, result);
                    }
                    token.ThrowIfCancellationRequested();
                    if (sourceSucceeded is false) {
                        copiedAllSources = false;
                    }
                    if (sourceCopied) {
                        copiedAnySources = true;
                        await refresh.Work(token);
                    }
                }
            }
            await refresh.Work(final: true, token);
            return new(copiedAnySources, copiedAllSources);
        }

        private static bool HasUnsafeStreamTarget(IEntryStreamSource source, TProvider provider) {
            var sourceIsUri = Uri.TryCreate(source.EntryID, UriKind.Absolute, out var sourceUri) &&
                              sourceUri.IsAbsoluteUri;
            if (!sourceIsUri) {
                return false;
            }
            sourceUri = ResourceIdentity(source, sourceUri);
            var providerUri = ResourceIdentity(provider, provider.Uri);
            var relativePath = source.RelativePath ?? "";
            if (source.SourceDirectory is not null) {
                if (FileProtocolUri.CouldBeSameOrDescendant(sourceUri, providerUri)) {
                    return true;
                }
                if (string.IsNullOrEmpty(relativePath)) {
                    return false;
                }
                var destinationDirectory = FileProtocolUri.Extend(
                    providerUri,
                    [relativePath],
                    includeTrailingSlash: true);
                return ConflictsWithDestination(source, sourceUri, destinationDirectory);
            }
            if (string.IsNullOrEmpty(relativePath)) {
                return false;
            }
            var destination = FileProtocolUri.Extend(
                providerUri,
                [relativePath],
                includeTrailingSlash: false);
            return ConflictsWithDestination(source, sourceUri, destination);
        }

        private static Uri ResourceIdentity(IEntryStreamSource source, Uri uri) {
            var isFileProtocolStream = source is FileProtocolStreamSource<TEntry>;
            if (isFileProtocolStream) {
                var stream = (FileProtocolStreamSource<TEntry>)source;
                if (stream.FileProtocolEntry is IFileProtocolResourceIdentity identity) {
                    return identity.GetResourceIdentity(uri);
                }
            }
            return uri;
        }

        private static Uri ResourceIdentity(TProvider provider, Uri uri) {
            if (provider is IFileProtocolResourceIdentity identity) {
                return identity.GetResourceIdentity(uri);
            }
            return uri;
        }

        private static bool ConflictsWithDestination(IEntryStreamSource source, Uri sourceUri, Uri destinationUri) {
            return source.SourceDirectory is not null
                ? FileProtocolUri.CouldBeSameOrDescendant(sourceUri, destinationUri)
                : FileProtocolUri.CouldBeSameResource(sourceUri, destinationUri);
        }

        private static bool HasUnsafeTarget(
            IReadOnlyList<PreparedStreamSet> preparedSets,
            TProvider provider,
            IReadOnlyList<ProvidedFile> files,
            IReadOnlyList<string> directoryPaths) {
            var sources = preparedSets.SelectMany(set => set.Sources).ToList();
            var hasUnsafeStreamTarget = sources.Any(source => HasUnsafeStreamTarget(source, provider));
            if (hasUnsafeStreamTarget) {
                return true;
            }
            var providerUri = ResourceIdentity(provider, provider.Uri);
            foreach (var file in files) {
                var destination = FileProtocolUri.Extend(
                    providerUri,
                    [file.RelativePath],
                    includeTrailingSlash: false);
                var targetConflictsWithSource = sources.Any(source => ConflictsWithRemoteSource(source, destination));
                if (targetConflictsWithSource) {
                    return true;
                }
            }
            foreach (var relativePath in directoryPaths) {
                var destination = FileProtocolUri.Extend(
                    providerUri,
                    [relativePath],
                    includeTrailingSlash: true);
                var targetConflictsWithSource = sources.Any(source => ConflictsWithRemoteSource(source, destination));
                if (targetConflictsWithSource) {
                    return true;
                }
            }
            return false;
        }

        private static bool ConflictsWithRemoteSource(IEntryStreamSource source, Uri destination) {
            var sourceIsUri = Uri.TryCreate(source.EntryID, UriKind.Absolute, out var sourceUri) &&
                              sourceUri.IsAbsoluteUri;
            if (!sourceIsUri) {
                return false;
            }
            sourceUri = ResourceIdentity(source, sourceUri);
            return ConflictsWithDestination(source, sourceUri, destination);
        }

        private static IReadOnlyList<string> LocalDirectoryRelativePaths(IReadOnlyList<string> sourcePaths,
                                                                         IReadOnlyList<ProvidedFile> files,
                                                                         CancellationToken token) {
            var roots = new List<string>();
            foreach (var sourcePath in sourcePaths) {
                token.ThrowIfCancellationRequested();
                var fullPath = Path.GetFullPath(sourcePath);
                var isDirectory = Directory.Exists(fullPath);
                var isIncluded = roots.Contains(fullPath, StringComparer.OrdinalIgnoreCase);
                var shouldInclude = isDirectory && !isIncluded;
                if (shouldInclude) {
                    roots.Add(fullPath);
                }
            }
            if (roots.Count == 0) {
                return [];
            }

            var directories = new List<string>();
            var seenDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in roots) {
                var pending = new Stack<DirectoryInfo>();
                var rootInfo = new DirectoryInfo(root);
                var rootIsLink = (rootInfo.Attributes & FileAttributes.ReparsePoint) != 0;
                if (!rootIsLink) {
                    pending.Push(rootInfo);
                }
                while (pending.Count > 0) {
                    token.ThrowIfCancellationRequested();
                    var directory = pending.Pop();
                    if (!seenDirectories.Add(directory.FullName)) {
                        continue;
                    }
                    directories.Add(directory.FullName);
                    foreach (var child in directory.EnumerateDirectories()) {
                        var childIsLink = (child.Attributes & FileAttributes.ReparsePoint) != 0;
                        if (!childIsLink) {
                            pending.Push(child);
                        }
                    }
                }
            }

            string commonRoot;
            if (files.Count > 0) {
                var firstFile = files.First();
                commonRoot = Path.GetFullPath(firstFile.OriginalPath);
                var relativeParts = firstFile.RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                foreach (var _ in relativeParts) {
                    var parentDirectory = Directory.GetParent(commonRoot);
                    if (parentDirectory is null) {
                        throw new InvalidDataException("The flattened file path has no local parent directory.");
                    }
                    commonRoot = parentDirectory.FullName;
                }
            }
            else {
                var parentPaths = roots
                    .Select(Path.GetDirectoryName)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .ToList();
                commonRoot = parentPaths.FirstOrDefault() ?? Path.GetPathRoot(roots[0]);
                foreach (var parentPath in parentPaths.Skip(1)) {
                    while (!IsSameOrWithinDirectory(parentPath, commonRoot)) {
                        var parentDirectory = Directory.GetParent(commonRoot);
                        if (parentDirectory is null) {
                            break;
                        }
                        commonRoot = parentDirectory.FullName;
                    }
                }
            }

            var relativePaths = new List<string>();
            foreach (var directory in directories) {
                var containsFile = files.Any(file =>
                    IsSameOrWithinDirectory(Path.GetFullPath(file.OriginalPath), directory));
                if (containsFile) {
                    continue;
                }

                var directoryIsWithinCommonRoot = IsSameOrWithinDirectory(directory, commonRoot);
                var directoryRoot = commonRoot;
                if (!directoryIsWithinCommonRoot) {
                    var sourceRoot = roots.First(root => IsSameOrWithinDirectory(directory, root));
                    directoryRoot = Path.GetDirectoryName(sourceRoot) ?? Path.GetPathRoot(sourceRoot);
                }
                var relativePath = Path.GetRelativePath(directoryRoot, directory);
                if (relativePath == ".") {
                    continue;
                }
                relativePaths.Add(relativePath
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/'));
            }
            return relativePaths;
        }

        private static bool IsSameOrWithinDirectory(string path, string directory) {
            var sameDirectory = string.Equals(path, directory, StringComparison.OrdinalIgnoreCase);
            if (sameDirectory) {
                return true;
            }
            var directoryPrefix = Path.EndsInDirectorySeparator(directory)
                ? directory
                : directory + Path.DirectorySeparatorChar;
            return path.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureNoReparsePoints(IReadOnlyList<string> paths, CancellationToken token) {
            var pending = new Stack<string>(paths);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (pending.Count > 0) {
                token.ThrowIfCancellationRequested();
                var path = pending.Pop();
                if (string.IsNullOrEmpty(path)) {
                    continue;
                }
                var fullPath = Path.GetFullPath(path);
                if (!visited.Add(fullPath)) {
                    continue;
                }
                FileAttributes attributes;
                try {
                    attributes = File.GetAttributes(fullPath);
                }
                catch (FileNotFoundException) {
                    continue;
                }
                catch (DirectoryNotFoundException) {
                    continue;
                }
                if ((attributes & FileAttributes.ReparsePoint) != 0) {
                    throw new NotSupportedException(
                        $"Transfers of local reparse points are not supported: {fullPath}");
                }
                if ((attributes & FileAttributes.Directory) == 0) {
                    continue;
                }
                foreach (var child in Directory.EnumerateFileSystemEntries(fullPath)) {
                    pending.Push(child);
                }
            }
        }

        private async Task<bool> CopyFromLocalDirectories(IReadOnlyList<string> relativePaths,
                                                           TProvider provider,
                                                           IReadOnlyCollection<string> existingDirectories,
                                                           CancellationToken token) {
            if (relativePaths.Count == 0) {
                return false;
            }
            var createdDirectories = new HashSet<string>(existingDirectories, StringComparer.Ordinal);
            var refresh = new ProviderPostponedRefresh(provider);
            foreach (var relativePath in relativePaths) {
                token.ThrowIfCancellationRequested();
                await CreateParentDirectories(provider, relativePath, createdDirectories, refresh, token);
                var destinationPath = FileProtocolUri.CombinePath(provider.Uri, relativePath);
                if (createdDirectories.Add(destinationPath)) {
                    token.ThrowIfCancellationRequested();
                    await EnsureDirectory(provider, destinationPath, token);
                    await refresh.Work(token);
                }
            }
            await refresh.Work(final: true, token);
            return true;
        }

        protected abstract Task CreateDirectory(TProvider provider, string path, CancellationToken token);

        /// <summary>
        /// Ensures that the destination path exists as a directory.
        /// </summary>
        /// <remarks>
        /// An existing directory should be accepted as already ensured. The default implementation delegates to
        /// <see cref="CreateDirectory"/> to preserve existing provider behavior. Override this method when the
        /// provider handles existing directories separately.
        /// </remarks>
        /// <param name="provider">The provider that owns the destination.</param>
        /// <param name="path">The destination path to ensure.</param>
        /// <param name="token">The token used to cancel the operation.</param>
        protected virtual Task EnsureDirectory(TProvider provider, string path, CancellationToken token) {
            return CreateDirectory(provider, path, token);
        }

        protected abstract Task<Stream> Write(TProvider provider, string path, long length, CancellationToken token);

        async Task<bool> IWorkProvidedIO.Work(IEnumerable<IProvidedIO> io,
                                              IProvider target,
                                              IOperationProgress progress,
                                              CancellationToken token) {
            ArgumentNullException.ThrowIfNull(io);
            if (target is not TProvider provider) {
                return false;
            }
            progress?.Change(kind: OperationProgressKind.FileSize);
            var providedIO = io.ToList();
            var streams = providedIO.StreamSets();
            var preparedSets = await PrepareStreamSets(streams, token);
            try {
                var fileSources = providedIO.FileSources();
                EnsureNoReparsePoints(fileSources, token);
                var files = await providedIO.FlattenFiles(progress, token);
                var directoryPaths = LocalDirectoryRelativePaths(fileSources, files, token);
                if (Log.Info()) {
                    Log.Info(Log.Join(
                        "Upload starting",
                        FileProtocolUri.Redact(provider.Uri),
                        "files", files.Count,
                        "directories", directoryPaths.Count,
                        "stream sets", preparedSets.Count));
                }
                if (HasUnsafeTarget(preparedSets, provider, files, directoryPaths)) {
                    if (Log.Warn()) {
                        Log.Warn(Log.Join(
                            "Upload refused; the target overlaps a source",
                            FileProtocolUri.Redact(provider.Uri)));
                    }
                    return false;
                }
                var copied = false;
                var succeeded = true;
                var existingDirectories = new HashSet<string>(StringComparer.Ordinal);
                if (files.Count > 0) {
                    var result = await CopyFromFiles(files, provider, progress, token);
                    copied = result.Copied;
                    succeeded &= result.Succeeded;
                    if (result.Copied) {
                        foreach (var file in files) {
                            foreach (var path in ParentDirectories(provider, file.RelativePath)) {
                                existingDirectories.Add(path);
                            }
                        }
                    }
                }
                if (directoryPaths.Count > 0) {
                    var copiedDirectories = await CopyFromLocalDirectories(
                        directoryPaths,
                        provider,
                        existingDirectories,
                        token);
                    copied = copied || copiedDirectories;
                }
                if (preparedSets.Count > 0) {
                    var result = await CopyFromStreams(preparedSets, provider, progress, token);
                    copied = copied || result.Copied;
                    succeeded &= result.Succeeded;
                }
                if (Log.Info()) {
                    Log.Info(Log.Join(
                        "Upload finished",
                        FileProtocolUri.Redact(provider.Uri),
                        "copied", copied,
                        "succeeded", succeeded));
                }
                return copied && succeeded;
            }
            finally {
                foreach (var set in preparedSets) {
                    set.Dispose();
                }
            }
        }
    }
}
