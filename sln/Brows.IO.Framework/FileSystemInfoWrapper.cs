using System;
using System.Collections.Generic;
using System.IO;
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

        private static IDictionary<string, IEntryData> DefaultData =>
            _DefaultData ?? (
            _DefaultData = Default.Data());
        private static IDictionary<string, IEntryData> _DefaultData;

        private FileSystemInfoWrapper(FileSystemInfo info, CancellationToken cancellationToken) {
            Info = info;
            File = Info as FileInfo;
            Directory = Info as DirectoryInfo;
            CancellationToken = cancellationToken;
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
                                var
                                info = Info;
                                info?.Refresh();
                                info = info?.Exists == true ? info : null;
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

        public static IReadOnlyDictionary<string, IEntryDataConverter> Converters =>
            _Converters ?? (
            _Converters = new Dictionary<string, IEntryDataConverter> {
                { nameof(Length), EntryDataConverter.FileSystemSize },
                { nameof(DirectorySize), EntryDataConverter.FileSystemSize }
            });
        private static IReadOnlyDictionary<string, IEntryDataConverter> _Converters;

        public IDictionary<string, IEntryData> Data() {
            return new Dictionary<string, IEntryData> {
                { Entry.ThumbnailKey, Entry.ThumbnailData },
                { nameof(Attributes), new DataItem(nameof(Attributes), this, i => i.Attributes) },
                { nameof(CreationTime), new DataItem(nameof(CreationTime), this, i => i.CreationTime) },
                { nameof(CreationTimeUtc), new DataItem(nameof(CreationTimeUtc), this, i => i.CreationTimeUtc) },
                { nameof(Extension), new DataItem(nameof(Extension), this, i => i.Extension) },
                { nameof(LastAccessTime), new DataItem(nameof(LastAccessTime), this, i => i.LastAccessTime) },
                { nameof(LastAccessTimeUtc), new DataItem(nameof(LastAccessTimeUtc), this, i => i.LastAccessTimeUtc) },
                { nameof(LastWriteTime), new DataItem(nameof(LastWriteTime), this, i => i.LastWriteTime) },
                { nameof(LastWriteTimeUtc), new DataItem(nameof(LastWriteTimeUtc), this, i => i.LastWriteTimeUtc) },
                { nameof(Length), new DataItem(nameof(Length), this, i => i.Length) },
                { nameof(LinkTarget), new DataItem(nameof(LinkTarget), this, i => i.LinkTarget) },
                { nameof(Name), new DataItem(nameof(Name), this, i => i.Name) },
                { nameof(FileInfoExtension.ChecksumMD5), new FileChecksum(nameof(FileInfoExtension.ChecksumMD5), File, CancellationToken) },
                { nameof(FileInfoExtension.ChecksumSHA1), new FileChecksum(nameof(FileInfoExtension.ChecksumSHA1), File, CancellationToken) },
                { nameof(FileInfoExtension.ChecksumSHA256), new FileChecksum(nameof(FileInfoExtension.ChecksumSHA256), File, CancellationToken) },
                { nameof(FileInfoExtension.ChecksumSHA512), new FileChecksum(nameof(FileInfoExtension.ChecksumSHA512), File, CancellationToken) },
                { nameof(DirectorySize), new DirectorySize(Directory, CancellationToken) }
            };
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
                base.Refresh();
            }
        }
    }
}
