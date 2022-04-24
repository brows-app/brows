using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using PATH = System.IO.Path;

namespace Brows {
    using IO;
    using Logger;
    using Threading.Tasks;

    public abstract class FileSystemProvider : EntryProvider<FileSystemEntry> {
        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(FileSystemProvider)));
        private ILog _Log;

        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<FileSystemProvider>());
        private TaskHandler _TaskHandler;

        private async Task<FileSystemEntry> Create(string path, CancellationToken cancellationToken) {
            var file = await FileInfoExtension.TryNewAsync(path, cancellationToken);
            if (file != null) {
                var exists = await file.ExistsAsync(cancellationToken);
                if (exists) {
                    return Create(file, cancellationToken);
                }
            }
            var directory = await DirectoryInfoExtension.TryNewAsync(path, cancellationToken);
            if (directory != null) {
                var exists = await directory.ExistsAsync(cancellationToken);
                if (exists) {
                    return Create(directory, cancellationToken);
                }
            }
            return null;
        }

        private async IAsyncEnumerable<FileSystemInfo> EnumerateFileSystemInfosAsync([EnumeratorCancellation] CancellationToken cancellationToken) {
            var searchPattern = "*";
            var enumerationOptions = new EnumerationOptions {
                AttributesToSkip = 0
            };
            await foreach (var info in Info.EnumerateFileSystemInfosAsync(searchPattern, enumerationOptions, Options.Enumerable, cancellationToken)) {
                var directory = info as DirectoryInfo;
                if (directory != null) {
                    if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint) == false) {
                        yield return directory;
                    }
                }
                else {
                    yield return info;
                }
            }
        }

        private async Task HandleThisEventAsync(FileSystemEventArgs e, CancellationToken cancellationToken) {
            if (null == e) throw new ArgumentNullException(nameof(e));
            if (Log.Info()) {
                Log.Info(
                    nameof(HandleThisEventAsync),
                    nameof(e.ChangeType) + " > " + e.ChangeType,
                    nameof(e.FullPath) + " > " + e.FullPath,
                    nameof(e.Name) + " > " + e.Name);
            }

            var changeType = e.ChangeType;
            var oldName = e is RenamedEventArgs r ? r.OldName : null;
            var name = e.Name;

            switch (changeType) {
                case WatcherChangeTypes.Changed: {
                        var entry = Existing.FirstOrDefault(e => e.Name == name);
                        if (entry != null) entry.Refresh(EntryRefresh.All);
                        break;
                    }
                case WatcherChangeTypes.Created: {
                        var entry = await Create(PATH.Combine(Path, name), cancellationToken);
                        await Provide(entry, cancellationToken);
                        break;
                    }
                case WatcherChangeTypes.Deleted: {
                        var entry = Existing.FirstOrDefault(e => e.Name == name);
                        if (entry != null) {
                            await Revoke(entry, cancellationToken);
                        }
                        break;
                    }
                case WatcherChangeTypes.Renamed: {
                        var oldEntry = Existing.FirstOrDefault(e => e.Name == oldName);
                        if (oldEntry != null) {
                            await Revoke(oldEntry, cancellationToken);
                        }
                        var entry = await Create(PATH.Combine(Path, name), cancellationToken);
                        await Provide(entry, cancellationToken);
                        break;
                    }
            }
        }

        private async Task HandleParentEventAsync(FileSystemEventArgs e, CancellationToken cancellationToken) {
            if (null == e) throw new ArgumentNullException(nameof(e));
            if (Log.Info()) {
                Log.Info(
                    nameof(HandleParentEventAsync),
                    nameof(e.ChangeType) + " > " + e.ChangeType,
                    nameof(e.FullPath) + " > " + e.FullPath,
                    nameof(e.Name) + " > " + e.Name);
            }

            switch (e.ChangeType) {
                case WatcherChangeTypes.Changed:
                case WatcherChangeTypes.Deleted:
                    var directory = Info;
                    await directory.RefreshAsync(cancellationToken);

                    var exists = await directory.ExistsAsync(cancellationToken);
                    if (exists == false) {
                        while (directory != null && await directory.ExistsAsync(cancellationToken) == false) {
                            directory = await directory.ParentAsync(cancellationToken);
                        }
                        if (directory != null) {
                            var browser = Browser;
                            if (browser != null) {
                                await browser.Browse(directory.FullName, cancellationToken);
                            }
                        }
                    }
                    break;
                case WatcherChangeTypes.Renamed:
                    var parent = ParentID;
                    if (parent != null) {
                        var newPath = PATH.Combine(parent, e.Name);
                        var browser = Browser;
                        if (browser != null) {
                            await browser.Browse(newPath, cancellationToken);
                        }
                    }
                    break;
            }
        }

        private void ThisNotifier_Notified(object sender, FileSystemEventArgs e) {
            TaskHandler.Begin(HandleThisEventAsync(e, default));
        }

        private void ParentNotifier_Notified(object sender, FileSystemEventArgs e) {
            TaskHandler.Begin(HandleParentEventAsync(e, default));
        }

        protected string Path { get; }
        protected DirectoryInfo Info { get; }

        protected FileSystemProvider(DirectoryInfo info) {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            Path = Info.FullName;
        }

        protected abstract FileSystemEntry Create(FileSystemInfo info, CancellationToken cancellationToken);

        protected override async Task BeginAsync(CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(BeginAsync));
            }
            var parent = await Info.ParentAsync(cancellationToken);
            if (parent == null) {
                ParentID = DriveProvider.DriveProviderID;
            }
            else {
                var exists = await parent.ExistsAsync(cancellationToken);
                if (exists) {
                    var parentDirectory = ParentID = parent.FullName;
                    var
                    parentNotifier = new FileSystemNotifier();
                    parentNotifier.Path = parentDirectory;
                    parentNotifier.Filter = Info.Name;
                    parentNotifier.Notified += ParentNotifier_Notified;
                    parentNotifier.Begin(cancellationToken);
                }
            }
            var
            thisNotifier = new FileSystemNotifier();
            thisNotifier.Path = Info.FullName;
            thisNotifier.Notified += ThisNotifier_Notified;
            thisNotifier.Begin(cancellationToken);

            await Log.Performance(Info.FullName, async () => {
                var infos = EnumerateFileSystemInfosAsync(cancellationToken);
                await foreach (var info in infos) {
                    var entry = Create(info, cancellationToken);
                    await Provide(entry, cancellationToken);
                }
            });
        }

        protected override async Task RefreshAsync(CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(RefreshAsync));
            }
            var dict = Existing.ToDictionary(entry => entry.Info.FullName, entry => entry);
            var infos = EnumerateFileSystemInfosAsync(cancellationToken);
            await foreach (var info in infos) {
                if (dict.Remove(info.FullName, out var value)) {
                    value.Refresh(EntryRefresh.All);
                }
                else {
                    value = Create(info, cancellationToken);
                    await Provide(value, cancellationToken);
                }
            }
            foreach (var item in dict) {
                await Revoke(item.Value, cancellationToken);
            }
        }

        public override IPanelID PanelID =>
            _PanelID ?? (
            _PanelID = new FileSystemPanelID(Info));
        private IPanelID _PanelID;

        public override IComponentResourceKey DataKeyResolver => FileSystemEntryData.Resolver;
        public override IReadOnlySet<string> DataKeyDefaults => FileSystemEntryData.Defaults;
        public override IReadOnlySet<string> DataKeyOptions => FileSystemEntryData.Options;
        public override IReadOnlyDictionary<string, IEntryDataConverter> DataKeyConverters => FileSystemEntryData.Converters;

        public FileSystemProviderOptions Options =>
            _Options ?? (
            _Options = new FileSystemProviderOptions { });
        private FileSystemProviderOptions _Options;

        public override IBookmark Bookmark =>
            _Bookmark ?? (
            _Bookmark = new PathBookmark());
        private IBookmark _Bookmark;

        public override string CreatedID(string createdName) {
            return PATH.Combine(Path, createdName);
        }
    }
}
