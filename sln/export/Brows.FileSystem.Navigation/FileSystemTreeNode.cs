using Brows.Exports;
using Brows.FileSystem;
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
    public sealed class FileSystemTreeNode : ProviderNavigationNode, IFileSystemInfo {
        private static readonly ILog Log = Logging.For(typeof(FileSystemTreeNode));

        private DriveInfo Drive { get; }
        private FileSystemEventTask FileSystemEvent;

        private FileSystemTreeNode(FileSystemInfo info, IFileSystemNavigationService service) : this(service) {
            Info = info;
        }

        private FileSystemTreeNode(DriveInfo drive, IFileSystemNavigationService service) : this(drive?.RootDirectory, service) {
            Drive = drive;
        }

        protected sealed override void Unloaded() {
            using (FileSystemEvent) {
                FileSystemEvent = null;
            }
        }

        protected sealed override void Collapsed() {
            using (FileSystemEvent) {
                FileSystemEvent = null;
            }
        }

        protected sealed override void Expanded() {
            if (Info is DirectoryInfo directory) {
                try {
                    FileSystemEvent = FileSystemEvent ?? FileSystemEventTasks.Add(directory.FullName, token: Token, task: (e, t) => {
                        switch (e?.ChangeType) {
                            case WatcherChangeTypes.Created:
                            case WatcherChangeTypes.Deleted:
                            case WatcherChangeTypes.Renamed:
                                Refresh();
                                break;
                        }
                        return Task.CompletedTask;
                    });
                }
                catch (Exception ex) {
                    if (Log.Info()) {
                        Log.Info(ex);
                    }
                }
            }
        }

        protected sealed override bool PartOf(string id) {
            if (Info == null) {
                return true;
            }
            var destination = id;
            for (; ; ) {
                var dir = Path.GetDirectoryName(destination);
                if (dir == "") return false;
                if (dir == null) return false;
                if (dir == ID) {
                    return true;
                }
                destination = dir;
            }
        }

        protected sealed override async Task<object> GetIcon(CancellationToken token) {
            if (Info == null) {
                return null;
            }
            var obj = default(object);
            var hint = new FileSystemIconHint {
                DirectoryOpen = Info is DirectoryInfo
                    ? Expand == true
                    : null
            };
            var task = Drive != null
                ? Service.DriveIcon?.Work(Drive, set: result => obj = result, token)
                : Service.FileSystemIcon?.Work(Info, hint, set: result => obj = result, token);
            if (task == null) {
                return null;
            }
            var work = await task;
            if (work == false) {
                return null;
            }
            return obj;
        }

        protected sealed override async IAsyncEnumerable<ProviderNavigationNode> EnumerateChildren([EnumeratorCancellation] CancellationToken token) {
            if (Drive is not null) {
                if (Drive.IsReady == false) {
                    yield break;
                }
            }
            var info = Info;
            if (info is null) {
                var drives = await Task.Run(cancellationToken: token, function: () => DriveInfo.GetDrives());
                foreach (var drive in drives) {
                    yield return new FileSystemTreeNode(drive, Service);
                }
            }
            if (info is DirectoryInfo directory) {
                var children = await Task.Run(cancellationToken: token, function: () => {
                    try {
                        return directory.GetDirectories("*", new EnumerationOptions());
                    }
                    catch (Exception ex) {
                        if (Log.Info()) {
                            Log.Info(ex);
                        }
                        return Array.Empty<DirectoryInfo>();
                    }
                });
                foreach (var child in children) {
                    yield return new FileSystemTreeNode(child, Service);
                }
            }
        }

        public IEntryStreamSource StreamSource =>
            new StreamSourceImplementation(this);

        public sealed override string Name => Drive?.Name ?? Info?.Name ?? "";
        public sealed override string ID => Drive?.RootDirectory?.FullName ?? Info?.FullName ?? "";

        public FileSystemInfo Info { get; }
        public IFileSystemNavigationService Service { get; }

        public FileSystemTreeNode(IFileSystemNavigationService service) {
            Service = service ?? throw new ArgumentNullException(nameof(service));
        }

        private sealed class StreamSourceImplementation : IEntryStreamSource {
            public string EntryID => Node.Info?.FullName ?? Node.Name;
            public string EntryName => Node.Name;
            public string RelativePath => Node.Info?.Name;
            public string SourceFile => Node.Info?.FullName;
            public string SourceDirectory => null;
            public long StreamLength => 0;
            public Stream Stream => null;

            public FileSystemTreeNode Node { get; }

            public StreamSourceImplementation(FileSystemTreeNode node) {
                Node = node ?? throw new ArgumentNullException(nameof(node));
            }

            public Task<IEntryStreamReady> StreamReady(CancellationToken token) {
                return Task.FromResult<IEntryStreamReady>(new EntryStreamReady());
            }
        }

        public sealed class StreamSet : EntryStreamSet {
            protected override IEnumerable<IEntryStreamSource> StreamSource() {
                return Collection;
            }

            protected sealed override IEnumerable<string> FileSource() {
                return Collection.Select(s => s.SourceFile);
            }

            public IEnumerable<IEntryStreamSource> Collection { get; }

            public StreamSet(IEnumerable<IEntryStreamSource> collection) {
                Collection = collection ?? throw new ArgumentNullException(nameof(collection));
            }
        }
    }
}
