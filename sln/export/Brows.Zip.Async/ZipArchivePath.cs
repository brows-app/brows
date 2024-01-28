using Brows.IO.Compression;
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

namespace Brows {
    public sealed class ZipArchivePath {
        private static readonly ILog Log = Logging.For(typeof(ZipArchivePath));
        private static readonly Dictionary<string, ZipArchivePath> Set = new();

        private int ReferenceCount;
        private DateTime LastWriteTimeUtc;

        private readonly ArchiveLocker Locker;
        private readonly FileSystemEventTask FileEvent;

        private TaskCache<ZipEntryInfoCollection> EntryInfoCache {
            get => _EntryInfoCache ?? (_EntryInfoCache = new(EntryInfoLoad));
            set => _EntryInfoCache = value;
        }
        private TaskCache<ZipEntryInfoCollection> _EntryInfoCache;

        private ZipArchivePath(FileInfo file, ZipArchiveNest nest, string fullName) {
            Nest = nest ?? throw new ArgumentNullException(nameof(nest));
            File = file ?? throw new ArgumentNullException(nameof(file));
            FileEvent = FileSystemEventTasks.Add(File.DirectoryName, FileEventTask);
            Locker = ArchiveLocker.Get(file);
            FullName = fullName;
            LastWriteTimeUtc = File.LastWriteTimeUtc;
        }

        private async Task<ZipEntryInfoCollection> EntryInfoLoad(CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(Read) + " > " + FullName);
            }
            using (await Locker.Lock(cancellationToken)) {
                return await Task.Run(cancellationToken: cancellationToken, function: () => {
                    using (var archive = Archive.Open(this, ZipArchiveMode.Read, cancellationToken)) {
                        if (cancellationToken.IsCancellationRequested) {
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                        var fileSet = new Dictionary<string, ZipEntryInfo>();
                        var pathSet = new Dictionary<string, ZipEntryInfo>();
                        var entries = archive.Zip.Entries;
                        foreach (var entry in entries) {
                            if (cancellationToken.IsCancellationRequested) {
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                            if (entry != null) {
                                if (Log.Info()) {
                                    Log.Info(nameof(entry) + " > " + entry.FullName);
                                }
                                var key = new ZipEntryName(entry.FullName);
                                var dir = entry.IsDirectory();
                                if (dir) {
                                    pathSet[key.Normalized] = ZipEntryInfo.Path(this, key);
                                }
                                else {
                                    fileSet[key.Normalized] = ZipEntryInfo.File(this, key, entry);
                                }
                            }
                        }
                        foreach (var item in fileSet) {
                            if (cancellationToken.IsCancellationRequested) {
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                            foreach (var path in item.Value.Name.Paths) {
                                if (cancellationToken.IsCancellationRequested) {
                                    cancellationToken.ThrowIfCancellationRequested();
                                }
                                if (pathSet.TryGetValue(path, out _) == false) {
                                    pathSet[path] = ZipEntryInfo.Path(this, new ZipEntryName(path));
                                }
                            }
                        }
                        return new ZipEntryInfoCollection(
                            items: pathSet.Values.Concat(fileSet.Values));
                    }
                });
            }
        }

        private ZipArchivePath Referenced() {
            if (Log.Info()) {
                Log.Info(nameof(Referenced) + " > " + FullName + " > " + ReferenceCount);
            }
            ReferenceCount++;
            return this;
        }

        private static string MakeFullName(FileInfo file, ZipArchiveNest nest) {
            if (null == file) throw new ArgumentNullException(nameof(file));
            if (null == nest) throw new ArgumentNullException(nameof(nest));
            return string.Join(">", nest.Prepend(file.FullName));
        }

        private async Task FileEventTask(FileSystemEventArgs e) {
            if (e != null) {
                var name = File.Name;
                var names = e is RenamedEventArgs r
                    ? new[] { e.Name, r.OldName }
                    : new[] { e.Name };
                if (names.Contains(name, StringComparer.OrdinalIgnoreCase)) {
                    var file = File.FullName;
                    var update = await FileSystemTask.ExistingFile(file, CancellationToken.None);
                    if (update == null) {
                        EntryInfoCache = null;
                        await FileDeleted.All();
                    }
                    else {
                        if (LastWriteTimeUtc != update.LastWriteTimeUtc) {
                            LastWriteTimeUtc = update.LastWriteTimeUtc;
                            EntryInfoCache = null;
                            await FileChanged.All();
                        }
                    }
                }
            }
        }

        public ZipArchiveFileEvent FileChanged =>
            _FileChanged ?? (
            _FileChanged = new());
        private ZipArchiveFileEvent _FileChanged;

        public ZipArchiveFileEvent FileDeleted =>
            _FileDeleted ?? (
            _FileDeleted = new());
        private ZipArchiveFileEvent _FileDeleted;

        public string Parent =>
            _Parent ?? (
            _Parent = Nest.Count == 0
                ? File.DirectoryName
                : string.Join(">", Nest
                    .Select((s, i) => {
                        var last = i == Nest.Count - 1;
                        if (last == false) {
                            return s;
                        }
                        var name = new ZipEntryName(s);
                        var parent = name.Parent?.Trim() ?? "";
                        if (parent != "") {
                            return parent;
                        }
                        return null;
                    })
                    .Where(s => s != null)
                    .Prepend(File.FullName)));
        private string _Parent;

        public string FullName { get; }
        public string FileName =>
            File.FullName;

        public FileInfo File { get; }
        public ZipArchiveNest Nest { get; }

        public static ZipArchivePath Get(FileInfo file, ZipArchiveNest nest) {
            if (null == file) throw new ArgumentNullException(nameof(file));
            if (null == nest) throw new ArgumentNullException(nameof(nest));
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

        public async Task<ZipEntryInfoCollection> EntryInfo(CancellationToken cancellationToken) {
            return await EntryInfoCache.Ready(cancellationToken);
        }

        public async Task Read(ZipArchiveRead info, IOperationProgress progress, CancellationToken cancellationToken) {
            if (null == info) throw new ArgumentNullException(nameof(info));
            if (Log.Info()) {
                Log.Info(nameof(Read) + " > " + FullName);
            }
            var extract = new Dictionary<string, string>(info.ExtractEntriesToFiles);
            if (extract.Count == 0) {
                return;
            }
            if (progress != null) {
                progress.Change(setTarget: extract.Count);
            }
            using (await Locker.Lock(cancellationToken)) {
                await Task.Run(cancellationToken: cancellationToken, action: () => {
                    using (var archive = Archive.Open(this, ZipArchiveMode.Read, cancellationToken)) {
                        foreach (var name in extract.Keys) {
                            if (cancellationToken.IsCancellationRequested) {
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                            if (progress != null) {
                                progress.Change(data: name);
                            }
                            var entry = archive.Zip.GetEntry(name);
                            if (entry != null) {
                                if (Log.Info()) {
                                    Log.Info(nameof(ZipFileExtensions.ExtractToFile) + " > " + entry.FullName);
                                }
                                entry.ExtractToFile(
                                    destinationFileName: extract[name],
                                    overwrite: info.ExtractOverwrites);
                            }
                            if (progress != null) {
                                progress.Change(1);
                            }
                        }
                    }
                });
            }
        }

        public async Task Update(ZipArchiveUpdate info, IOperationProgress progress, CancellationToken token) {
            if (null == info) throw new ArgumentNullException(nameof(info));
            if (Log.Info()) {
                Log.Info(nameof(Update) + " > " + FullName);
            }
            var delete = new HashSet<string>(info.DeleteEntries.Where(i => i != null));
            var create = new Dictionary<string, string>(info.CreateEntriesFromFiles);
            var stream = new List<IEntryStreamSet>(info.CopyStreamSets.Where(i => i != null));
            if (stream.Count == 0 && create.Count == 0 && delete.Count == 0) {
                return;
            }
            if (progress != null) {
                progress.Change(addTarget: stream.Count + create.Count + delete.Count);
            }
            using (await Locker.Lock(token)) {
                await Task.Run(cancellationToken: token, action: () => {
                    using (var archive = Archive.Open(this, ZipArchiveMode.Update, token)) {
                        foreach (var name in delete) {
                            if (token.IsCancellationRequested) {
                                token.ThrowIfCancellationRequested();
                            }
                            if (progress != null) {
                                progress.Change(data: name);
                            }
                            if (Log.Info()) {
                                Log.Info(nameof(delete) + " > " + name);
                            }
                            archive.Zip.Delete(name);
                            if (progress != null) {
                                progress.Change(1);
                            }
                        }
                        foreach (var entry in create) {
                            if (token.IsCancellationRequested) {
                                token.ThrowIfCancellationRequested();
                            }
                            if (progress != null) {
                                progress.Change(data: entry.Key);
                            }
                            if (Log.Info()) {
                                Log.Info(nameof(ZipFileExtensions.CreateEntryFromFile) + " > " + entry.Key + " > " + entry.Value);
                            }
                            archive.Zip.Delete(entry.Key);
                            archive.Zip.CreateEntryFromFile(
                                entryName: entry.Key,
                                sourceFileName: entry.Value);
                            if (progress != null) {
                                progress.Change(1);
                            }
                        }
                    }
                });
            }
            foreach (var set in stream) {
                await Task.Run(cancellationToken: token, function: async () => {
                    await set.ConsumeFromMemory(progress: progress, token: token, consuming: async (source, stream, progress, token) => {
                        using (await Locker.Lock(token)) {
                            using (var archive = Archive.Open(this, ZipArchiveMode.Update, token)) {
                                var name = source.RelativePath
                                    .Trim()
                                    .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                    .Replace(Path.DirectorySeparatorChar, '/')
                                    .Replace(Path.AltDirectorySeparatorChar, '/');
                                archive.Zip.Delete(name);
                                if (stream != null) {
                                    var entry = archive.Zip.CreateEntry(name);
                                    await using var opened = entry.Open();
                                    await stream.CopyToAsync(opened, token);
                                }
                            }
                        }
                    });
                });
            }
        }

        public async Task<bool> Drop(IPanelDrop data, IOperationProgress progress, CancellationToken cancellationToken) {
            if (data == null) {
                return false;
            }
            var copy = data.CopyFiles ?? Array.Empty<string>();
            var move = data.MoveFiles ?? Array.Empty<string>();
            var files = new HashSet<string>(copy.Concat(move));
            if (files.Count == 0) {
                return false;
            }
            await Update(token: cancellationToken, progress: progress, info: new() {
                CreateEntriesFromFiles = files.ToDictionary(
                    file => Path.GetFileName(file),
                    file => file)
            });
            return true;
        }

        public abstract class StreamSource<TEntry> : EntryStreamSource<TEntry> where TEntry : IEntry {
            private Archive Archive;
            private ArchiveLocked Locked;

            private class EntryNotFoundException : Exception {
            }

            private sealed class Disposable : IEntryStreamReady {
                public StreamSource<TEntry> Source { get; }

                public Disposable(StreamSource<TEntry> source) {
                    Source = source ?? throw new ArgumentNullException(nameof(source));
                }

                void IDisposable.Dispose() {
                    Source.Archive?.Dispose();
                    Source.Archive = null;
                    Source.Locked?.Dispose();
                    Source.Locked = null;
                }
            }

            protected ZipEntryInfo EntryInfo { get; }

            protected StreamSource(TEntry entry, ZipEntryInfo entryInfo) : base(entry) {
                EntryInfo = entryInfo ?? throw new ArgumentNullException(nameof(entryInfo));
            }

            protected sealed override Task<IEntryStreamReady> StreamReady(CancellationToken token) {
                return Task.FromResult<IEntryStreamReady>(new Disposable(this));
            }

            protected sealed override Stream Stream() {
                if (EntryInfo.Kind == ZipEntryKind.File) {
                    Locked = EntryInfo.Archive.Locker.Lock();
                    Archive = Archive.Open(EntryInfo.Archive, ZipArchiveMode.Read, CancellationToken.None);
                    var entry = Archive.Zip.GetEntry(EntryInfo.Name.Original);
                    if (entry == null) {
                        throw new EntryNotFoundException();
                    }
                    return entry.Open();
                }
                return null;
            }

            public sealed override long StreamLength =>
                EntryInfo.SizeOriginal ?? 0;
        }
    }
}
