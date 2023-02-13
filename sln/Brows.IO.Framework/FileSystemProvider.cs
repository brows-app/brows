using Domore.Collections.Generic;
using Domore.IO;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PATH = System.IO.Path;

namespace Brows {
    using IO;
    using Threading.Tasks;

    public abstract class FileSystemProvider : EntryProvider<FileSystemEntry> {
        private static readonly ILog Log = Logging.For(typeof(FileSystemProvider));
        private static readonly FileSystemColumn Column = new FileSystemColumn();

        private FileSystemEvent ThisEvent;
        private FileSystemEvent ParentEvent;

        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<FileSystemProvider>());
        private TaskHandler _TaskHandler;

        private async Task<FileSystemEntry> Create(string path, CancellationToken cancellationToken) {
            var file = await FileSystem.FileExists(path, cancellationToken);
            if (file != null) {
                return Create(file, cancellationToken);
            }
            var directory = await FileSystem.DirectoryExists(path, cancellationToken);
            if (directory != null) {
                return Create(directory, cancellationToken);
            }
            return null;
        }

        private async Task HandleThisEventAsync(FileSystemEventArgs e, CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(HandleThisEventAsync));
            }
            if (e == null) {
                return;
            }
            var r = e as RenamedEventArgs;
            if (r != null) {
                if (Provided.HasName(r.OldName, out var oldEntry)) {
                    if (await oldEntry.Exists(cancellationToken) == false) {
                        await Revoke(oldEntry, cancellationToken);
                    }
                }
            }
            var changedName = e.Name;
            var changedPath = e.FullPath;
            var changedInfo = await FileSystem.InfoExists(changedPath, cancellationToken);
            var entry = default(FileSystemEntry);
            var entryExists = Provided.HasName(changedName, out entry);
            if (entryExists) {
                if (await entry.Exists(cancellationToken) == false) {
                    await Revoke(entry, cancellationToken);
                    entryExists = false;
                }
            }
            if (changedInfo == null) {
                return;
            }
            if (entryExists == false) {
                entry = Create(changedInfo, cancellationToken);
                await Provide(entry, cancellationToken);
                return;
            }
            var info = entry.Info;
            if (info.LastWriteTimeUtc == changedInfo.LastWriteTimeUtc) {
                entry.Refresh(EntryRefresh.Data);
                return;
            }
            entry.Refresh(EntryRefresh.All);
        }

        private async Task HandleParentEvent(FileSystemEventArgs e, CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(HandleParentEvent));
            }
            if (e == null) return;
            if (e.ChangeType == WatcherChangeTypes.Renamed) {
                var oldName = e is RenamedEventArgs renamed ? renamed.OldName : null;
                var caseSensitive = await CaseSensitive(cancellationToken);
                var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                var isThis = Info.Name.Equals(oldName, comparison);
                if (isThis) {
                    var newPath = PATH.Combine(ParentID, e.Name);
                    var browser = Browser;
                    if (browser != null) {
                        await browser.Browse(newPath, cancellationToken);
                        return;
                    }
                }
            }
            var exists = await Async.With(cancellationToken).Run(() => {
                Info.Refresh();
                return Info.Exists;
            });
            if (exists == false) {
                var existing = await Async.With(cancellationToken).Run(() => {
                    var directory = Info;
                    while (directory != null && directory.Exists == false) {
                        directory = directory.Parent;
                    }
                    return directory?.FullName;
                });
                var browser = Browser;
                if (browser != null) {
                    await browser.Browse(existing, cancellationToken);
                }
            }
        }

        private void ThisEvent_Handler(object sender, FileSystemEventArgs e) {
            TaskHandler.Begin(HandleThisEventAsync(e, CancellationToken));
        }

        private void ParentEvent_Handler(object sender, FileSystemEventArgs e) {
            TaskHandler.Begin(HandleParentEvent(e, CancellationToken));
        }

        private IAsyncEnumerable<IReadOnlyList<T>> Collect<T>(Func<FileSystemInfo, T> factory, CancellationToken cancellationToken) {
            var enumeration = Info.EnumerateFileSystemInfos(
                searchPattern: "*",
                enumerationOptions: new EnumerationOptions {
                    AttributesToSkip = 0
                });
            var collect = enumeration.CollectAsync(
                cancellationToken: cancellationToken,
                options: new CollectAsyncOptions<FileSystemInfo, T> {
                    Mode = CollectAsyncMode.Channel,
                    Skip = info =>
                        info is DirectoryInfo directory &&
                        directory.Attributes.HasFlag(FileAttributes.ReparsePoint),
                    Ticks = 100,
                    Transform = factory
                });
            return collect;
        }

        protected string Path { get; }
        protected DirectoryInfo Info { get; }

        protected FileSystemProvider(DirectoryInfo info) {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            Path = Info.FullName;
        }

        protected abstract FileSystemEntry Create(FileSystemInfo info, CancellationToken cancellationToken);

        protected override Task End(CancellationToken cancellationToken) {
            if (ThisEvent != null) ThisEvent.Handler -= ThisEvent_Handler;
            if (ParentEvent != null) ParentEvent.Handler -= ParentEvent_Handler;
            return base.End(cancellationToken);
        }

        protected sealed override async Task Begin(CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(Begin));
            }
            var parent = await Async.With(cancellationToken).Run(() => Info.Parent);
            if (parent == null) {
                ParentID = DriveProvider.DriveProviderID;
            }
            else {
                var parentExists = await Async.With(cancellationToken).Run(() => parent.Exists);
                if (parentExists) {
                    var parentDirectory = ParentID = parent.FullName;
                    ParentEvent = new FileSystemEvent(parentDirectory);
                    ParentEvent.Handler += ParentEvent_Handler;
                }
            }
            ThisEvent = new FileSystemEvent(Path);
            ThisEvent.Handler += ThisEvent_Handler;
            var collect = Collect(info => Create(info, cancellationToken), cancellationToken);
            await foreach (var group in collect) {
                await Provide(group, cancellationToken);
            }
        }

        protected sealed override async Task Refresh(CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(Refresh));
            }
            var ids = Provided.IDSet();
            var collections = Collect(info => info, cancellationToken);
            await foreach (var collection in collections) {
                foreach (var info in collection) {
                    var id = info.FullName;
                    if (Provided.HasID(id, out var entry)) {
                        entry.Refresh(EntryRefresh.All);
                    }
                    else {
                        entry = Create(info, cancellationToken);
                        await Provide(entry, cancellationToken);
                    }
                    ids.Remove(id);
                }
            }
            foreach (var id in ids) {
                if (Provided.HasID(id, out var entry)) {
                    await Revoke(entry, cancellationToken);
                }
            }
        }

        protected sealed override async Task Init(CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(Init));
            }
            await Column.Init(cancellationToken);
        }

        public sealed override IPanelID PanelID =>
            _PanelID ?? (
            _PanelID = new FileSystemPanelID(Info));
        private IPanelID _PanelID;

        public sealed override string Directory =>
            PanelID.Value;

        public sealed override IReadOnlySet<string> DataKeyOptions =>
            Column.Options;

        public sealed override IReadOnlyDictionary<string, IEntryColumn> DataKeyColumns =>
            Column.Columns;

        public sealed override IReadOnlySet<string> DataKeyDefault =>
            Column.Default;

        public sealed override IReadOnlyDictionary<string, EntrySortDirection?> DataKeySorting =>
            Column.Sorting;

        public sealed override IBookmark Bookmark =>
            _Bookmark ?? (
            _Bookmark = new PathBookmark());
        private IBookmark _Bookmark;

        public sealed override string CreatedID(string createdName) {
            return PATH.Combine(Path, createdName);
        }
    }
}
