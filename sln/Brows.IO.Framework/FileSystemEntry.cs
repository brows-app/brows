using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Brows {
    using Config;
    using Gui;

    public abstract class FileSystemEntry : Entry, IFileSystemEntry {
        private IReadOnlyDictionary<string, IEntryData> Data =>
            _Data ?? (
            _Data = CreateData().ToDictionary(d => d.Key, d => d));
        private IReadOnlyDictionary<string, IEntryData> _Data;

        private PropSysConfig PropSys =>
            _PropSys ?? (
            _PropSys = PropSysConfig.Instance);
        private PropSysConfig _PropSys;

        private IEnumerable<IEntryData> CreateData() {
            var wrap = new FileSystemInfoWrapper(Info, PropertyProvider, ViewColumns, CancellationToken);
            var propSys = PropSys.For(wrap, CancellationToken);
            var infoData = FileInfoData.For(wrap, CancellationToken);
            var otherData = new IEntryData[] {
                Entry.ThumbnailData,
                new FileChecksum(nameof(IO.FileChecksum.ChecksumMD5), FileInfo, CancellationToken),
                new FileChecksum(nameof(IO.FileChecksum.ChecksumSHA1), FileInfo, CancellationToken),
                new FileChecksum(nameof(IO.FileChecksum.ChecksumSHA256), FileInfo, CancellationToken),
                new FileChecksum(nameof(IO.FileChecksum.ChecksumSHA512), FileInfo, CancellationToken),
                new DirectorySize(nameof(DirectorySize), DirectoryInfo, CancellationToken) { Alignment = EntryDataAlignment.Right, Converter = EntryDataConverter.FileSystemSize },
                new DirectoryFileCount(nameof(DirectoryFileCount), DirectoryInfo, CancellationToken) { Alignment = EntryDataAlignment.Right },
                new DirectoryDirectoryCount(nameof(DirectoryDirectoryCount), DirectoryInfo, CancellationToken){ Alignment = EntryDataAlignment.Right }
            };
            var allData = infoData.Concat(propSys).Concat(otherData);
            return allData;
        }

        protected abstract IFilePropertyProvider PropertyProvider { get; }

        protected override IReadOnlySet<string> Keys =>
            _Keys ?? (
            _Keys = new HashSet<string>(Data.Keys));
        private IReadOnlySet<string> _Keys;

        protected override IconInput IconInput {
            get {
                if (_IconInput == null) {
                    _IconInput = Kind switch {
                        FileSystemEntryKind.Directory => new IconInput(IconStock.Folder),
                        FileSystemEntryKind.File => new IconInput(IconStock.Unknown, ID),
                        _ => new IconInput(IconStock.Unknown),
                    };
                }
                return _IconInput;
            }
        }
        private IconInput _IconInput;

        protected override IEntryData Get(string key) {
            return Data[key];
        }

        public string Path { get; }
        public FileSystemInfo Info { get; }
        public FileSystemEntryKind Kind { get; }

        public FileInfo FileInfo => Info as FileInfo;
        public DirectoryInfo DirectoryInfo => Info as DirectoryInfo;

        public sealed override string ID => Info.FullName;
        public sealed override string Name => Info.Name;
        public sealed override string File => Info.FullName;

        public FileSystemEntry(FileSystemInfo info, CancellationToken cancellationToken) : base(cancellationToken) {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            Path = Info.FullName;
            Kind = Info is DirectoryInfo
                ? FileSystemEntryKind.Directory
                : FileSystemEntryKind.File;
        }
    }
}
