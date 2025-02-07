using Brows.Exports;
using Domore.IO;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
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
                    var path = Path.GetDirectoryName(Uri.AbsolutePath.TrimEnd('/'))?.Replace('\\', '/')?.TrimEnd('/') + '/';
                    var parent = new UriBuilder(Uri) { Path = path };
                    _Parent = parent.Equals(Uri)
                        ? ""
                        : parent.ToString();
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
                        }
                        if (entry.Kind == FileProtocolEntryKind.DirectoryLink) {
                            // TODO: Handle directory links.
                        }
                        if (entry.Kind == FileProtocolEntryKind.FileLink) {
                            // TODO: Handle file links.
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
                io.Add(new ProvidedIO { StreamSets = new[] { new FileProtocolStreamSet<TEntry>(recurse(token)) } });
                return true;
            }
        }

        protected abstract class CopyProvidedIO<TProvider> : ICopyProvidedIO, IProviderExport<TProvider> where TProvider : FileProtocolProvider<TEntry, TConfig> {
            private sealed class FileItem {
                public string RelativePath => _RelativePath ??=
                    Provided.RelativePath?.Trim()?.Trim('/') ?? "";
                private string _RelativePath;

                public string DestinationPath => _DestinationPath ??=
                    string.Join('/', Provider.Uri.LocalPath.TrimEnd('/'), RelativePath);
                private string _DestinationPath;

                public IReadOnlyList<string> DestinationPathParts => _DestinationPathParts ??=
                    DestinationPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                private IReadOnlyList<string> _DestinationPathParts;

                public string DestinationDirectory => _DestinationDirectory ??=
                    DestinationPathParts.Count > 1
                        ? '/' + string.Join('/', DestinationPathParts.Take(DestinationPathParts.Count - 1))
                        : "";
                private string _DestinationDirectory;

                public string OriginalPath =>
                    Provided.OriginalPath;

                public ProvidedFile Provided { get; }
                public TProvider Provider { get; }

                public FileItem(TProvider provider, ProvidedFile provided) {
                    Provided = provided ?? throw new ArgumentNullException(nameof(provided));
                    Provider = provider ?? throw new ArgumentNullException(nameof(provider));
                }
            }

            private async Task<bool> CopyFromFiles(IReadOnlyCollection<ProvidedFile> files, TProvider provider, IOperationProgress progress, CancellationToken token) {
                if (null == files) throw new ArgumentNullException(nameof(files));
                if (null == provider) throw new ArgumentNullException(nameof(provider));
                if (files.Count == 0) {
                    return false;
                }
                var items = files.Select(file => new FileItem(provider, file)).Where(item => !string.IsNullOrWhiteSpace(item.DestinationPath)).ToList();
                var dests = items.Select(item => item.DestinationDirectory).Where(dest => !string.IsNullOrWhiteSpace(dest)).Distinct();
                var refresh = new ProviderPostponedRefresh(provider);
                foreach (var dest in dests) {
                    await CreateDirectory(provider, dest, token);
                    await refresh.Work(token);
                }
                foreach (var item in items) {
                    var config = provider.Config;
                    var source = item.OriginalPath;
                    var sourceFile = await FileSystemTask.ExistingFile(source, token);
                    if (sourceFile == null) {
                        if (Log.Warn()) {
                            Log.Warn(Log.Join("Source file does not exist.", source));
                        }
                        continue;
                    }
                    await progress.Child(sourceFile.Name, OperationProgressKind.FileSize, async (progress, token) => {
                        var srcLength = sourceFile.Length;
                        var destination = item.DestinationPath;
                        progress.Change(setTarget: srcLength);
                        await Task.Run(cancellationToken: token, function: async () => {
                            using var dest = await Write(provider, destination, srcLength, token);
                            await using var fileSource = sourceFile.OpenRead();
                            await progress.Copy(
                                source: fileSource,
                                destination: dest,
                                token: token);
                        });
                    });
                    await refresh.Work(token);
                }
                await refresh.Work(final: true, token);
                return true;
            }

            private async Task<bool> CopyFromStreams(IReadOnlyList<IEntryStreamSet> entryStreamSets, TProvider provider, IOperationProgress progress, CancellationToken token) {
                if (null == provider) throw new ArgumentNullException(nameof(provider));
                if (null == entryStreamSets) throw new ArgumentNullException(nameof(entryStreamSets));
                if (entryStreamSets.Count == 0) {
                    return false;
                }
                await Task.WhenAll(entryStreamSets.Select(set => Task.Run(cancellationToken: token, function: async () => {
                    await set.Consume(progress: progress, token: token, consuming: async (source, stream, progress, token) => {
                        var relativePath = source.RelativePath?.Trim()?.Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? "";
                        if (relativePath != "") {
                            var path = string.Join('/', provider.Uri.LocalPath.TrimEnd('/'), relativePath);
                            var config = provider.Config;
                            if (stream != null) {
                                using var scpSendStream = await Write(provider, path, source.StreamLength, token);
                                await stream.CopyToAsync(scpSendStream, token);
                            }
                        }
                    });
                })));
                return true;
            }

            protected abstract Task CreateDirectory(TProvider provider, string path, CancellationToken token);
            protected abstract Task<Stream> Write(TProvider provider, string path, long length, CancellationToken token);

            async Task<bool> IWorkProvidedIO.Work(IEnumerable<IProvidedIO> io, IProvider target, IOperationProgress progress, CancellationToken token) {
                if (io is null) throw new ArgumentNullException(nameof(io));
                if (target is not TProvider provider) {
                    return false;
                }
                var files = await io.FlattenFiles(progress, token);
                if (files.Count > 0) {
                    return await CopyFromFiles(files, provider, progress, token);
                }
                var streams = io.StreamSets();
                if (streams.Count > 0) {
                    return await CopyFromStreams(streams, provider, progress, token);
                }
                return false;
            }
        }
    }
}
