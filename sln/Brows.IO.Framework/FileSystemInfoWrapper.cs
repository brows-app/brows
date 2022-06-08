using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using IO;
    using Logger;
    using Threading.Tasks;

    internal class FileSystemInfoWrapper {
        private readonly ILog Log = Logging.For(typeof(FileSystemInfoWrapper));
        private readonly FileInfo File;
        private readonly DirectoryInfo Directory;
        private readonly FileSystemInfo Info;
        private readonly CancellationToken CancellationToken;
        private readonly object Locker = new object();

        private bool Refreshed;
        private bool Refreshing;
        private bool RefreshInfo;
        private FileAttributes? Attributes;
        private DateTime? CreationTime;
        private DateTime? CreationTimeUtc;
        private string Extension;
        private DateTime? LastAccessTime;
        private DateTime? LastAccessTimeUtc;
        private DateTime? LastWriteTime;
        private DateTime? LastWriteTimeUtc;
        private long? Length;
        private string LinkTarget;
        private string Name;

        private static FileSystemInfoWrapper Default { get; } = new FileSystemInfoWrapper(null, default);

        private static IReadOnlyDictionary<string, IEntryData> DefaultData =>
            _DefaultData ?? (
            _DefaultData = Default.Data());
        private static IReadOnlyDictionary<string, IEntryData> _DefaultData;

        private FileSystemInfoWrapper(FileSystemInfo info, CancellationToken cancellationToken) {
            Info = info;
            File = Info as FileInfo;
            Directory = Info as DirectoryInfo;
            CancellationToken = cancellationToken;
        }

        private static IEnumerable<(IEntryData Data, double Width)> ColumnTable(FileSystemInfoWrapper item) {
            if (null == item) throw new ArgumentNullException(nameof(item));
            return new List<(IEntryData, double)> {
                (Entry.ThumbnailData, 100),
                (new DataItem(nameof(Attributes), item, i => i.Attributes), 75),
                (new DataItem(nameof(CreationTime), item, i => i.CreationTime) { Converter = EntryDataConverter.DateTime }, 175),
                (new DataItem(nameof(CreationTimeUtc), item, i => i.CreationTimeUtc) { Converter = EntryDataConverter.DateTime }, 175),
                (new DataItem(nameof(Extension), item, i => i.Extension), 50),
                (new DataItem(nameof(LastAccessTime), item, i => i.LastAccessTime) { Converter = EntryDataConverter.DateTime }, 175),
                (new DataItem(nameof(LastAccessTimeUtc), item, i => i.LastAccessTimeUtc) { Converter = EntryDataConverter.DateTime }, 175),
                (new DataItem(nameof(LastWriteTime), item, i => i.LastWriteTime) { Converter = EntryDataConverter.DateTime }, 175),
                (new DataItem(nameof(LastWriteTimeUtc), item, i => i.LastWriteTimeUtc) { Converter = EntryDataConverter.DateTime }, 175),
                (new DataItem(nameof(Length), item, i => i.Length) { Alignment = EntryDataAlignment.Right, Converter = EntryDataConverter.FileSystemSize }, 75),
                (new DataItem(nameof(LinkTarget), item, i => i.LinkTarget), 100),
                (new DataItem(nameof(Name), item, i => i.Name), 250),
                (new FileChecksum(nameof(FileInfoExtension.ChecksumMD5), item.File, item.CancellationToken), 250),
                (new FileChecksum(nameof(FileInfoExtension.ChecksumSHA1), item.File, item.CancellationToken), 250),
                (new FileChecksum(nameof(FileInfoExtension.ChecksumSHA256), item.File, item.CancellationToken), 250),
                (new FileChecksum(nameof(FileInfoExtension.ChecksumSHA512), item.File, item.CancellationToken), 250),
                (new DirectorySize(item.Directory, item.CancellationToken) { Alignment = EntryDataAlignment.Right, Converter = EntryDataConverter.FileSystemSize }, 75)
            };
        }

        private async Task<object> Get(Func<FileSystemInfoWrapper, object> func) {
            if (null == func) throw new ArgumentNullException(nameof(func));
            if (Refreshed == false) {
                await Async.Run(CancellationToken, () => {
                    lock (Locker) {
                        if (Refreshed == false) {
                            Refreshed = true;
                            Refreshing = true;
                            if (Log.Debug()) {
                                Log.Debug(
                                    nameof(Refreshing),
                                    $"{nameof(Info)} > {Info}");
                            }
                            try {
                                var info = Info;

                                if (RefreshInfo) {
                                    RefreshInfo = false;
                                    info?.Refresh();
                                }
                                var infoExists = info?.Exists;
                                if (infoExists != true) {
                                    info = null;
                                }
                                var file = info as FileInfo;
                                Attributes = info?.Attributes;
                                CreationTime = info?.CreationTime;
                                CreationTimeUtc = info?.CreationTimeUtc;
                                Extension = file?.Extension;
                                LastAccessTime = info?.LastAccessTime;
                                LastAccessTimeUtc = info?.LastAccessTimeUtc;
                                LastWriteTime = info?.LastWriteTime;
                                LastWriteTimeUtc = info?.LastWriteTimeUtc;
                                Length = file?.Length;
                                LinkTarget = info?.LinkTarget;
                                Name = info?.Name;
                            }
                            finally {
                                Refreshing = false;
                            }
                        }
                    }
                });
            }
            return func(this);
        }

        public static IReadOnlySet<string> Keys =>
            _Keys ?? (
            _Keys = new HashSet<string>(DefaultData.Keys));
        private static IReadOnlySet<string> _Keys;

        public static IReadOnlyDictionary<string, IEntryColumn> Columns =>
            _Columns ?? (
            _Columns = ColumnTable(Default).ToDictionary(
                c => c.Data.Key,
                c => (IEntryColumn)new EntryColumn {
                    Resolver = FileSystemEntryData.Resolver,
                    Width = c.Width
                }));
        private static IReadOnlyDictionary<string, IEntryColumn> _Columns;

        public IReadOnlyDictionary<string, IEntryData> Data() {
            return ColumnTable(this).ToDictionary(
                c => c.Data.Key,
                c => c.Data);
        }

        public static FileSystemInfoWrapper For(FileSystemInfo info, CancellationToken cancellationToken) {
            return new FileSystemInfoWrapper(info, cancellationToken);
        }

        private sealed class DataItem : EntryData {
            protected sealed override Task<object> Access() {
                return Wrap.Get(Func);
            }

            public string Name { get; }
            public FileSystemInfoWrapper Wrap { get; }
            public Func<FileSystemInfoWrapper, object> Func { get; }

            public DataItem(string name, FileSystemInfoWrapper wrap, Func<FileSystemInfoWrapper, object> func) : base(name) {
                Name = name;
                Func = func;
                Wrap = wrap ?? throw new ArgumentNullException(nameof(wrap));
            }

            public sealed override void Refresh() {
                Wrap.Refreshed = false;
                Wrap.RefreshInfo = true;
                base.Refresh();
            }
        }
    }
}
