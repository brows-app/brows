using Brows.Export;
using Brows.Exports;
using Domore.IO;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DIRECTORY = System.IO.Directory;
using PATH = System.IO.Path;

namespace Brows {
    internal sealed class FileSystemProvider : EntryProvider<FileSystemEntry, FileSystemConfig> {
        private static readonly ILog Log = Logging.For(typeof(FileSystemProvider));
        private static readonly EnumerationOptions EnumerationOptions = new EnumerationOptions {
            AttributesToSkip = 0,
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false
        };

        private readonly StringComparer SetComparer;
        private readonly Dictionary<string, FileSystemEntry> IDSet;
        private readonly Dictionary<string, FileSystemEntry> NameSet;
        private readonly Dictionary<string, List<FileSystemEntry>> ExtensionSet;

        private FileSystemEventTask ThisEvent;
        private FileSystemEventTask ParentEvent;

        private async Task<FileSystemEntry> Create(string path, CancellationToken token) {
            var existing = await FileSystemTask.Existing(path, token);
            if (existing != null) {
                return new FileSystemEntry(this, existing);
            }
            return null;
        }

        private async Task ThisEventTask(FileSystemEventArgs e) {
            if (Log.Debug()) {
                Log.Debug(nameof(ThisEventTask), ID, e?.ChangeType, e?.FullPath);
            }
            if (e == null) {
                return;
            }
            switch (e.ChangeType) {
                case WatcherChangeTypes.Changed:
                    if (NameSet.TryGetValue(e.Name, out var changedEntry)) {
                        changedEntry.RefreshAfter(delay: Config.FileSystemEvent.RefreshDelay);
                    }
                    break;
                case WatcherChangeTypes.Created:
                    if (Log.Info()) {
                        Log.Info(nameof(WatcherChangeTypes.Created) + " > " + e.FullPath);
                    }
                    var idCreated = e.FullPath;
                    if (IDSet.TryGetValue(idCreated, out var createdEntry) == false) {
                        var createdInfo = await FileSystemTask.Existing(idCreated, Token);
                        if (createdInfo != null) {
                            await Provide(new FileSystemEntry(this, createdInfo));
                        }
                    }
                    break;
                case WatcherChangeTypes.Deleted:
                    if (Log.Info()) {
                        Log.Info(nameof(WatcherChangeTypes.Deleted) + " > " + e.FullPath);
                    }
                    if (NameSet.TryGetValue(e.Name, out var deletedEntry)) {
                        await Revoke(deletedEntry);
                    }
                    break;
                case WatcherChangeTypes.Renamed:
                    if (Log.Info()) {
                        Log.Info(nameof(WatcherChangeTypes.Renamed) + " > " + e.FullPath);
                    }
                    var r = e as RenamedEventArgs;
                    if (r != null) {
                        if (NameSet.TryGetValue(r.OldName, out var oldEntry)) {
                            await Revoke(oldEntry);
                        }
                        var idNew = e.FullPath;
                        if (IDSet.TryGetValue(idNew, out var newEntry) == false) {
                            var newInfo = await FileSystemTask.Existing(idNew, Token);
                            if (newInfo != null) {
                                await Provide(new FileSystemEntry(this, newInfo));
                            }
                        }
                    }
                    break;
            }
        }

        private async Task ParentEventTask(FileSystemEventArgs e) {
            if (Log.Debug()) {
                Log.Debug(nameof(ParentEventTask), ID, e?.ChangeType, e?.FullPath);
            }
            if (e == null) {
                return;
            }
            switch (e.ChangeType) {
                case WatcherChangeTypes.Renamed:
                    var oldName = e is RenamedEventArgs renamed ? renamed.OldName : null;
                    if (oldName != null) {
                        var isThisDir = Directory.Name.Equals(oldName, CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                        if (isThisDir) {
                            var newPath = PATH.Combine(Parent, e.Name);
                            await Change(newPath, Token);
                            return;
                        }
                    }
                    break;
                case WatcherChangeTypes.Deleted:
                    var name = e.Name;
                    if (name != null) {
                        var isThisDir = Directory.Name.Equals(name, CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                        if (isThisDir) {
                            var exists = await Task.Run(cancellationToken: Token, function: () => DIRECTORY.Exists(ID));
                            if (exists == false) {
                                var existing = await Task.Run(cancellationToken: Token, function: () => {
                                    var directory = Directory;
                                    while (directory != null && directory.Exists == false) {
                                        directory = directory.Parent;
                                    }
                                    return directory?.FullName;
                                });
                                await Change(existing, Token);
                                return;
                            }
                        }
                    }
                    break;
            }
        }

        private ChannelReader<T> Read<T>(Func<FileSystemInfo, T> factory, Action done, CancellationToken token) {
            if (factory is null) throw new ArgumentNullException(nameof(factory));
            var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions {
                SingleReader = true,
                SingleWriter = true
            });
            var reader = channel.Reader;
            var writer = channel.Writer;
            Task.Run(cancellationToken: token, function: async () => {
                var error = default(Exception);
                try {
                    var enumeration = Directory.EnumerateFileSystemInfos(
                        searchPattern: "*",
                        enumerationOptions: new EnumerationOptions {
                            AttributesToSkip = 0,
                            IgnoreInaccessible = true,
                            RecurseSubdirectories = false,
                            ReturnSpecialDirectories = false
                        });
                    foreach (var info in enumeration) {
                        if (token.IsCancellationRequested) {
                            token.ThrowIfCancellationRequested();
                        }
                        var ignore =
                            info.Attributes.HasFlag(FileAttributes.Directory) &&
                            info.Attributes.HasFlag(FileAttributes.ReparsePoint);
                        if (ignore) {
                            continue;
                        }
                        var item = factory(info);
                        await writer.WriteAsync(item);
                    }
                }
                catch (Exception ex) {
                    error = ex;
                }
                finally {
                    writer.Complete(error);
                    done?.Invoke();
                }
            });
            return reader;
        }

        protected sealed override IReadOnlyCollection<IEntryDataDefinition> DataDefinition =>
            Factory.Metadata.DataDefinition;

        protected sealed override void Adding(IReadOnlyCollection<FileSystemEntry> entries) {
            foreach (var entry in entries) {
                if (entry.Info is FileInfo file) {
                    _ = Factory.Metadata.Ready(file.FullName, Token);
                    if (ExtensionSet.TryGetValue(entry.Extension, out var extension) == false) {
                        ExtensionSet[entry.Extension] = extension = new();
                    }
                    extension.Add(entry);
                }
                IDSet[entry.ID] = entry;
                NameSet[entry.Name] = entry;
            }
        }

        protected sealed override void Removing(IReadOnlyCollection<FileSystemEntry> entries) {
            foreach (var entry in entries) {
                if (ExtensionSet.TryGetValue(entry.Extension, out var extension)) {
                    extension.Remove(entry);
                }
                IDSet.Remove(entry.ID);
                NameSet.Remove(entry.Name);
            }
        }

        protected sealed override void DragSelected(object source) {
            var service = Factory.DragSourceFileSystemInfos;
            if (service != null) {
                service.Drag(source, Provided
                    .Where(entry => entry.Select)
                    .Select(entry => entry.Info));
            }
        }

        protected sealed override Task<bool> Drop(IPanelDrop data, IOperationProgress operationProgress, CancellationToken token) {
            var service = Factory.DropDirectoryInfoData;
            if (service != null) {
                return service.Work(Directory, data, operationProgress, token);
            }
            return Task.FromResult(false);
        }

        protected sealed override void Dispose(bool disposing) {
            if (disposing) {
                using (ThisEvent)
                using (ParentEvent) {
                }
            }
            base.Dispose(disposing);
        }

        protected sealed override async Task Begin(CancellationToken token) {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(Begin), ID));
            }
            ThisEvent = FileSystemEventTasks.Add(ID, ThisEventTask);
            ParentEvent = Parent != null && Parent != Drives.ID && (await FileSystemTask.ExistingDirectory(Parent, token) != null)
                ? FileSystemEventTasks.Add(Parent, ParentEventTask)
                : null;
            var delay = Math.Max(0, Config.ProvideDelay);
            var reader = FileSystemReader.Read(Directory, "*", EnumerationOptions, info => new FileSystemEntry(this, info), token);
            await reader.Wait(delay, token);
            await Provide(reader.Existing());
            await foreach (var item in reader.Remaining(token)) {
                await Provide(item);
            }
        }

        protected sealed override async Task Refresh(CancellationToken token) {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(Refresh), ID));
            }
            var ids = new Dictionary<string, FileSystemEntry>(IDSet, SetComparer);
            var read = FileSystemReader.Read(Directory, "*", EnumerationOptions, info => info, token);
            await foreach (var info in read.Remaining(token)) {
                var id = info.FullName;
                if (ids.TryGetValue(id, out var entry)) {
                    ids.Remove(id);
                    entry.Refresh(delayed: false);
                }
                else {
                    await Provide(new FileSystemEntry(this, info));
                }
            }
            await Revoke(ids.Values);
        }

        public object Entry =>
            _Entry ?? (
            _Entry = new FileSystemEntry(this, Directory));
        private object _Entry;

        public bool CaseSensitive { get; }

        public new FileSystemConfig Config =>
            base.Config;

        public new IEnumerable<string> ObservedKeys =>
            base.ObservedKeys;

        public sealed override string Parent =>
            _Parent ?? (
            _Parent = Directory.Parent?.FullName ?? Drives.ID);
        private string _Parent;

        public DirectoryInfo Directory { get; }
        public FileSystemProviderFactory Factory { get; }

        public FileSystemProvider(FileSystemProviderFactory factory, DirectoryInfo directory, bool caseSensitive) : base(directory?.FullName) {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Directory = directory ?? throw new ArgumentNullException(nameof(directory));
            CaseSensitive = caseSensitive;
            SetComparer = CaseSensitive
                ? StringComparer.Ordinal
                : StringComparer.OrdinalIgnoreCase;
            IDSet = new(SetComparer);
            NameSet = new(SetComparer);
            ExtensionSet = new(StringComparer.OrdinalIgnoreCase);
        }

        public bool SuggestKey(string key) {
            foreach (var extension in ExtensionSet) {
                if (extension.Value.Count > 0) {
                    if (Factory.Metadata.File.TryGetValue(extension.Key, out var set)) {
                        if (set.Contains(key)) {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private sealed class ProvideIO : IProvideIO, IEntryProviderExport<FileSystemProvider> {
            public async Task<bool> Work(ICollection<IProvidedIO> io, IEntryProvider target, IOperationProgress progress, CancellationToken token) {
                if (io is null) throw new ArgumentNullException(nameof(io));
                if (target is not FileSystemProvider provider) {
                    return false;
                }
                var selection = provider.Selected.OfType<FileSystemEntry>().ToList();
                if (selection.Count == 0) {
                    return false;
                }
                var streams = selection.Select(e => new FileSystemStreamSource(e));
                var streamSet = new FileSystemStreamSet(streams);
                io.Add(new ProvidedIO {
                    StreamSets = new[] { streamSet }
                });
                await Task.CompletedTask;
                return true;
            }
        }

        private sealed class MoveProvidedIO : IMoveProvidedIO, IEntryProviderExport<FileSystemProvider> {
            public async Task<bool> Work(IEnumerable<IProvidedIO> io, IEntryProvider target, IOperationProgress progress, CancellationToken token) {
                if (io is null) throw new ArgumentNullException(nameof(io));
                if (target is not FileSystemProvider provider) {
                    return false;
                }
                var files = io.Files();
                if (files.Count > 0) {
                    var service = provider.Factory.MoveFilesToDirectory;
                    if (service == null) {
                        return false;
                    }
                    return await service.Work(files, provider.Directory.FullName, progress, token);
                }
                return false;
            }
        }

        private sealed class CopyProvidedIO : ICopyProvidedIO, IEntryProviderExport<FileSystemProvider> {
            public async Task<bool> Work(IEnumerable<IProvidedIO> io, IEntryProvider target, IOperationProgress progress, CancellationToken token) {
                if (io is null) throw new ArgumentNullException(nameof(io));
                if (target is not FileSystemProvider provider) {
                    return false;
                }
                var files = io.Files();
                if (files.Count > 0) {
                    var service = provider.Factory.CopyFilesToDirectory;
                    if (service == null) {
                        return false;
                    }
                    return await service.Work(files, provider.Directory.FullName, progress, token);
                }
                var streams = io.StreamSets();
                if (streams.Count > 0) {
                    var directory = provider.Directory.FullName;
                    await Task.WhenAll(streams.Select(set => Task.Run(cancellationToken: token, function: async () => {
                        await set.Consume(progress: progress, token: token, consuming: async (source, stream, progress, token) => {
                            var relativePath = source.RelativePath?.Trim()?.Trim(PATH.DirectorySeparatorChar, PATH.AltDirectorySeparatorChar) ?? "";
                            if (relativePath != "") {
                                var ext = PATH.GetExtension(relativePath);
                                var fileInfo = new FileInfo(PATH.Combine(directory, relativePath));
                                for (; ; ) {
                                    if (fileInfo.Exists == false) {
                                        if (DIRECTORY.Exists(fileInfo.DirectoryName) == false) {
                                            DIRECTORY.CreateDirectory(fileInfo.DirectoryName);
                                        }
                                        break;
                                    }
                                    relativePath = relativePath + ".copy" + ext;
                                    fileInfo = new FileInfo(PATH.Combine(directory, relativePath));
                                }
                                await using (var file = fileInfo.Create()) {
                                    await stream.CopyToAsync(file, token);
                                }
                            }
                        });
                    })));
                    return true;
                }
                return false;
            }
        }

        private sealed class Bookmark : IBookmark, IEntryProviderExport<FileSystemProvider> {
            public async Task<bool> Work(IReadOnlyList<KeyValuePair<string, string>> existing, IList<KeyValuePair<string, string>> added, IEntryProvider target, IOperationProgress progress, CancellationToken token) {
                if (target is not FileSystemProvider provider) {
                    return false;
                }
                if (added is null) {
                    return false;
                }
                var sep = new[] { PATH.DirectorySeparatorChar, PATH.AltDirectorySeparatorChar };
                var dir = provider.Directory.FullName;
                var parts = dir
                    .Split(
                        sep,
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Reverse()
                    .ToList();
                var items = existing;
                var take = 1;
                var key = default(string);
                for (; ; ) {
                    var k = string.Join(PATH.DirectorySeparatorChar, parts.Take(take).Reverse());
                    var conflicts = existing.Where(item => k.Equals(item.Key, StringComparison.CurrentCultureIgnoreCase));
                    if (conflicts.Any()) {
                        var alreadyBookmarked = conflicts.Any(item => dir.Equals(item.Value, provider.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase));
                        if (alreadyBookmarked) {
                            return false;
                        }
                        if (take == parts.Count) {
                            return false;
                        }
                        take++;
                    }
                    else {
                        key = k;
                        break;
                    }
                }
                added.Add(KeyValuePair.Create(key ?? dir, dir));
                await Task.CompletedTask;
                return true;
            }
        }
    }
}