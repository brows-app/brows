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

        private Task Refreshed;
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
        private FileSystemEntryKind? Kind;

        private static FileSystemInfoWrapper Default { get; } = new FileSystemInfoWrapper(null, default);

        private static IReadOnlyDictionary<string, IEntryData> DefaultData =>
            _DefaultData ?? (
            _DefaultData = Default.Data());
        private static IReadOnlyDictionary<string, IEntryData> _DefaultData;

        private FileSystemInfoWrapper(FileSystemInfo info, CancellationToken cancellationToken) {
            Info = info;
            Name = Info?.Name;
            File = Info as FileInfo;
            Directory = Info as DirectoryInfo;
            Kind =
                File != null ? FileSystemEntryKind.File :
                Directory != null ? FileSystemEntryKind.Directory :
                FileSystemEntryKind.Unknown;
            CancellationToken = cancellationToken;
        }

        private static IEnumerable<(IEntryData Data, double Width)> ColumnTable(FileSystemInfoWrapper item) {
            if (null == item) throw new ArgumentNullException(nameof(item));
            return new List<(IEntryData, double)> {
                (Entry.ThumbnailData, 100),
                (DataItem.For(nameof(Attributes), item, i => i.Attributes), 75),
                (DataItem.For(nameof(CreationTime), item, i => i.CreationTime, converter: EntryDataConverter.DateTime), 175),
                (DataItem.For(nameof(CreationTimeUtc), item, i => i.CreationTimeUtc, converter: EntryDataConverter.DateTime), 175),
                (DataItem.For(nameof(Extension), item, i => i.Extension), 50),
                (DataItem.For(nameof(Kind), item, i => i.Kind, load: item.Kind, loaded: true), 75),
                (DataItem.For(nameof(LastAccessTime), item, i => i.LastAccessTime, converter: EntryDataConverter.DateTime), 175),
                (DataItem.For(nameof(LastAccessTimeUtc), item, i => i.LastAccessTimeUtc, converter: EntryDataConverter.DateTime), 175),
                (DataItem.For(nameof(LastWriteTime), item, i => i.LastWriteTime, converter: EntryDataConverter.DateTime), 175),
                (DataItem.For(nameof(LastWriteTimeUtc), item, i => i.LastWriteTimeUtc, converter: EntryDataConverter.DateTime), 175),
                (DataItem.For(nameof(Length), item, i => i.Length, converter: EntryDataConverter.FileSystemSize, alignment: EntryDataAlignment.Right), 75),
                (DataItem.For(nameof(LinkTarget), item, i => i.LinkTarget), 100),
                (DataItem.For(nameof(Name), item, i => i.Name, load: item.Name, loaded: true), 250),
                (new FileChecksum(nameof(FileInfoExtension.ChecksumMD5), item.File, item.CancellationToken), 250),
                (new FileChecksum(nameof(FileInfoExtension.ChecksumSHA1), item.File, item.CancellationToken), 250),
                (new FileChecksum(nameof(FileInfoExtension.ChecksumSHA256), item.File, item.CancellationToken), 250),
                (new FileChecksum(nameof(FileInfoExtension.ChecksumSHA512), item.File, item.CancellationToken), 250),
                (new DirectorySize(item.Directory, item.CancellationToken) { Alignment = EntryDataAlignment.Right, Converter = EntryDataConverter.FileSystemSize }, 75)
            };
        }

        private async Task<T> Get<T>(Func<FileSystemInfoWrapper, T> func) {
            if (null == func) throw new ArgumentNullException(nameof(func));
            if (Refreshed == null) {
                Refreshed = Async.Run(CancellationToken, () => {
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
                        var directory = info as DirectoryInfo;
                        Kind =
                            file != null ? FileSystemEntryKind.File :
                            directory != null ? FileSystemEntryKind.Directory :
                            FileSystemEntryKind.Unknown;
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
                });
            }
            await Refreshed;
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

        private static class DataItem {
            public static DataItem<T> For<T>(string name, FileSystemInfoWrapper wrap, Func<FileSystemInfoWrapper, T> func, T load = default, bool? loaded = null, IEntryDataConverter converter = null, EntryDataAlignment? alignment = null) {
                return new DataItem<T>(name, load, wrap, func) {
                    Alignment = alignment ?? EntryDataAlignment.Default,
                    Converter = converter,
                    Loaded = loaded ?? false
                };
            }
        }

        private sealed class DataItem<T> : EntryData<T> {
            protected sealed override async Task<T> Access(CancellationToken cancellationToken) {
                if (Loaded) {
                    return Load;
                }
                return await Wrap.Get(Func);
            }

            protected sealed override void Refresh() {
                Loaded = false;
                Wrap.Refreshed = null;
                Wrap.RefreshInfo = true;
            }

            public bool Loaded { get; set; }

            public T Load { get; }
            public string Name { get; }
            public FileSystemInfoWrapper Wrap { get; }
            public Func<FileSystemInfoWrapper, T> Func { get; }

            public DataItem(string name, T load, FileSystemInfoWrapper wrap, Func<FileSystemInfoWrapper, T> func) : base(name, wrap?.CancellationToken ?? default) {
                Load = load;
                Name = name;
                Func = func ?? throw new ArgumentNullException(nameof(func));
                Wrap = wrap ?? throw new ArgumentNullException(nameof(wrap));
            }
        }
    }
}
