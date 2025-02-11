using Brows.Export;
using Brows.Exports;
using Brows.FileSystem;
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
    internal sealed class FileSystemProvider : Provider<FileSystemEntry, FileSystemConfig>, IFileSystemNavigationProvider {
        private static readonly ILog Log = Logging.For(typeof(FileSystemProvider));

        private readonly StringComparer CaseComparer;

        private FileSystemEventTask ThisEvent;
        private FileSystemEventTask ParentEvent;

        private async Task<FileSystemEntry> Create(string path, CancellationToken token) {
            var existing = await FileSystemTask.Existing(path, token).ConfigureAwait(false);
            if (existing != null) {
                return new FileSystemEntry(this, existing);
            }
            return null;
        }

        private async Task ThisEventTask(FileSystemEventArgs e, CancellationToken token) {
            if (Log.Debug()) {
                Log.Debug(nameof(ThisEventTask), ID, e?.ChangeType, e?.FullPath);
            }
            if (e == null) {
                return;
            }
            switch (e.ChangeType) {
                case WatcherChangeTypes.Changed:
                    var changedEntry = Lookup(name: e.Name);
                    if (changedEntry != null) {
                        changedEntry.RefreshAfter(delay: Config.FileSystemEvent.RefreshDelay);
                    }
                    break;
                case WatcherChangeTypes.Created:
                    if (Log.Info()) {
                        Log.Info(Log.Join(nameof(WatcherChangeTypes.Created), e.FullPath));
                    }
                    var createdID = e.FullPath;
                    var createdEntry = Lookup(id: createdID);
                    if (createdEntry == null) {
                        var createdInfo = await FileSystemTask.Existing(createdID, Token);
                        if (createdInfo != null) {
                            await Provide(new FileSystemEntry(this, createdInfo), token);
                        }
                    }
                    break;
                case WatcherChangeTypes.Deleted:
                    if (Log.Info()) {
                        Log.Info(Log.Join(nameof(WatcherChangeTypes.Deleted), e.FullPath));
                    }
                    var deletedEntry = Lookup(name: e.Name);
                    if (deletedEntry != null) {
                        await Revoke(deletedEntry, token);
                    }
                    break;
                case WatcherChangeTypes.Renamed:
                    if (Log.Info()) {
                        Log.Info(Log.Join(nameof(WatcherChangeTypes.Renamed), e.FullPath));
                    }
                    var r = e as RenamedEventArgs;
                    if (r != null) {
                        var oldEntry = Lookup(name: r.OldName);
                        if (oldEntry != null) {
                            await Revoke(oldEntry, token);
                        }
                        var newID = e.FullPath;
                        var newEntry = Lookup(id: newID);
                        if (newEntry == null) {
                            var newInfo = await FileSystemTask.Existing(newID, Token);
                            if (newInfo != null) {
                                await Provide(new FileSystemEntry(this, newInfo), token);
                            }
                        }
                    }
                    break;
            }
        }

        private async Task ParentEventTask(FileSystemEventArgs e, CancellationToken token) {
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
                            await Change(newPath, token);
                            return;
                        }
                    }
                    break;
                default:
                    var name = e.Name;
                    if (name != null) {
                        var isThisDir = Directory.Name.Equals(name, CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                        if (isThisDir) {
                            var exists = await FileSystemTask.ExistingDirectory(Directory.FullName, token);
                            if (exists != null) {
                                return;
                            }
                            var existing = await Task.Run(cancellationToken: Token, function: () => {
                                var
                                directory = Directory;
                                directory.Refresh();
                                while (directory != null && directory.Exists == false) {
                                    directory = directory.Parent;
                                }
                                return directory?.FullName;
                            });
                            await Change(existing, Token);
                            return;
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

        protected sealed override void DragSelected(object source) {
            var service = Factory.DragSourceFileSystemInfos;
            if (service != null) {
                service.Drag(source, Provided
                    .Where(entry => entry.Select)
                    .Select(entry => entry.Info));
            }
        }

        protected sealed override async Task<bool> Drop(IPanelDrop data, IOperationProgress operationProgress, CancellationToken token) {
            var service = Factory.DropDirectoryInfoData;
            if (service != null) {
                if (data != null) {
                    var directory = data.Target is FileSystemEntry e && e.Info is DirectoryInfo d
                        ? d
                        : Directory;
                    return await service
                        .Work(directory, data, operationProgress, token)
                        .ConfigureAwait(false);
                }
            }
            return false;
        }

        protected sealed override async Task<bool> Take(IMessage message, CancellationToken token) {
            if (message is DeviceChange deviceChange) {
                if (deviceChange.Info is DeviceChangeVolume volume) {
                    var affected = await volume.Affects(Directory, token);
                    if (affected == true) {
                        await Change(Drives.ID, token).ConfigureAwait(false);
                    }
                }
                return true;
            }
            return false;
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
            ThisEvent = FileSystemEventTasks.Add(ID, ThisEventTask, token);
            ParentEvent = Parent != null && Parent != Drives.ID && (await FileSystemTask.ExistingDirectory(Parent, token) != null)
                ? FileSystemEventTasks.Add(Parent, ParentEventTask, token)
                : null;
            var delay = Math.Max(0, Config.ProvideDelay);
            var reader = FileSystemReader.Read(Directory, info => new FileSystemEntry(this, info), token);
            await reader.Wait(delay, token);
            await Provide(reader.Existing(), token);
            await foreach (var item in reader.Remaining(token)) {
                await Provide(item, token);
            }
        }

        protected sealed override async Task Refresh(CancellationToken token) {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(Refresh), ID));
            }
            var ids = Provided.ToDictionary(entry => entry.ID, CaseComparer);
            var read = FileSystemReader.Read(Directory, info => info, token);
            await foreach (var info in read.Remaining(token)) {
                var id = info.FullName;
                if (ids.TryGetValue(id, out var entry)) {
                    ids.Remove(id);
                    await entry.Refresh(delayed: false, token);
                }
                else {
                    await Provide(new FileSystemEntry(this, info), token);
                }
            }
            await Revoke(ids.Values, token);
        }

        public object Entry =>
            _Entry ?? (
            _Entry = new FileSystemEntry(this, Directory));
        private object _Entry;

        public bool CaseSensitive { get; }

        public new FileSystemConfig Config =>
            base.Config;

        public new IReadOnlyList<FileSystemEntry> Provided =>
            base.Provided;

        public new IEnumerable<FileSystemEntry> Selected =>
            base.Selected;

        public new IEntrySorting Sorting =>
            base.Sorting;

        public new IEnumerable<string> ObservedKeys =>
            base.ObservedKeys;

        public sealed override string Parent =>
            _Parent ?? (
            _Parent = Directory.Parent?.FullName ?? Drives.ID);
        private string _Parent;

        public DirectoryInfo Directory { get; }
        public FileSystemProviderFactory Factory { get; }

        public FileSystemProvider(FileSystemProviderFactory factory, DirectoryInfo directory, bool caseSensitive, int initialCapacity) : base(directory?.FullName, initialCapacity, caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase, caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase) {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Directory = directory ?? throw new ArgumentNullException(nameof(directory));
            CaseSensitive = caseSensitive;
            CaseComparer = CaseSensitive
                ? StringComparer.Ordinal
                : StringComparer.OrdinalIgnoreCase;
        }

        private sealed class ProvideIO : IProvideIO, IProviderExport<FileSystemProvider> {
            public async Task<bool> Work(ICollection<IProvidedIO> io, ICommandSource source, IProvider target, IOperationProgress progress, CancellationToken token) {
                if (io is null) throw new ArgumentNullException(nameof(io));
                if (source is null) return false;
                if (target is not FileSystemProvider provider) {
                    return false;
                }
                var added = false;
                var entries = source.Items?.OfType<FileSystemEntry>()?.ToList();
                if (entries?.Count > 0) {
                    var streams = entries.Select(e => new FileSystemStreamSource(e));
                    var streamSet = new FileSystemStreamSet(streams);
                    io.Add(new ProvidedIO { StreamSets = new[] { streamSet } });
                    added = true;
                }
                var treeNodes = source.Items?.OfType<FileSystemTreeNode>()?.ToList();
                if (treeNodes?.Count > 0) {
                    var streams = treeNodes.Select(n => n.StreamSource);
                    var streamSet = new FileSystemTreeNode.StreamSet(streams);
                    io.Add(new ProvidedIO { StreamSets = new[] { streamSet } });
                    added = true;
                }
                return await Task.FromResult(added);
            }
        }

        private sealed class MoveProvidedIO : IMoveProvidedIO, IProviderExport<FileSystemProvider> {
            public async Task<bool> Work(IEnumerable<IProvidedIO> io, IProvider target, IOperationProgress progress, CancellationToken token) {
                if (io is null) throw new ArgumentNullException(nameof(io));
                if (target is not FileSystemProvider provider) {
                    return false;
                }
                var files = io.FileSources();
                if (files.Count == 0) {
                    return false;
                }
                var task = provider.Factory.MoveFilesToDirectory?.Work(files, provider.Directory.FullName, progress, token);
                if (task == null) {
                    return false;
                }
                return await task;
            }
        }

        private sealed class CopyProvidedIO : ICopyProvidedIO, IProviderExport<FileSystemProvider> {
            public async Task<bool> Work(IEnumerable<IProvidedIO> io, IProvider target, IOperationProgress progress, CancellationToken token) {
                if (io is null) throw new ArgumentNullException(nameof(io));
                if (target is not FileSystemProvider provider) {
                    return false;
                }
                var files = io.FileSources();
                if (files.Count > 0) {
                    var service = provider.Factory.CopyFilesToDirectory;
                    if (service == null) {
                        return false;
                    }
                    return await service.Work(files, provider.Directory.FullName, progress, token);
                }
                if (progress != null) {
                    progress.Change(kind: OperationProgressKind.FileSize);
                }
                var streams = io.StreamSets();
                if (streams.Count == 0) {
                    return false;
                }
                var directory = provider.Directory.FullName;
                await Task.WhenAll(streams.Select(set => set.Consume(progress: progress, token: token, consuming: async (source, stream, progress, token) => {
                    var relativePath = source.RelativePath?.Trim()?.Trim(PATH.DirectorySeparatorChar, PATH.AltDirectorySeparatorChar) ?? "";
                    if (relativePath == "") {
                        return;
                    }
                    if (stream == null) {
                        var sourceDirectory = source.SourceDirectory;
                        if (sourceDirectory != null) {
                            var path = PATH.Combine(directory, relativePath);
                            await FileSystemTask.CreateDirectory(path, token);
                        }
                        return;
                    }
                    var fileInfo = await Task.Run(cancellationToken: token, function: () => {
                        var ext = PATH.GetExtension(relativePath);
                        var fileInfo = new FileInfo(PATH.Combine(directory, relativePath));
                        for (; ; ) {
                            if (fileInfo.Exists == false) {
                                if (DIRECTORY.Exists(fileInfo.DirectoryName) == false) {
                                    DIRECTORY.CreateDirectory(fileInfo.DirectoryName);
                                }
                                return fileInfo;
                            }
                            relativePath = relativePath + ".copy" + ext;
                            fileInfo = new FileInfo(PATH.Combine(directory, relativePath));
                        }
                    });
                    await progress.Child(relativePath, OperationProgressKind.FileSize, async (progress, token) => {
                        await using var destination = await Task.Run(cancellationToken: token, function: fileInfo.Create);
                        await progress.Copy(stream, destination, token);
                    });
                })));
                return true;
            }
        }

        private sealed class Bookmark : IBookmark, IProviderExport<FileSystemProvider> {
            public async Task<bool> Work(IReadOnlyList<KeyValuePair<string, string>> existing, IList<KeyValuePair<string, string>> added, IProvider target, IOperationProgress progress, CancellationToken token) {
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
