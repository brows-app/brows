using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Brows {
    using Gui;

    public abstract class FileSystemEntry : Entry, IFileSystemEntry {
        private IDictionary<string, IEntryData> Data =>
            _Data ?? (
            _Data = Wrap.Data());
        private IDictionary<string, IEntryData> _Data;

        internal FileSystemInfoWrapper Wrap =>
            _Wrap ?? (
            _Wrap = FileSystemInfoWrapper.For(Info, CancellationToken));
        private FileSystemInfoWrapper _Wrap;

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

        public override string ID => Info.FullName;
        public override string Name => Info.Name;
        public override string File => Info.FullName;

        public FileSystemEntry(FileSystemInfo info, CancellationToken cancellationToken) : base(cancellationToken) {
            Info = info ?? throw new ArgumentNullException(nameof(info)); ;
            Path = Info.FullName;
            Kind = Info is DirectoryInfo
                ? FileSystemEntryKind.Directory
                : FileSystemEntryKind.File;
        }

        public FileSystemEntry(string path, FileSystemEntryKind kind, CancellationToken cancellationToken) : base(cancellationToken) {
            Path = path;
            Kind = kind;
            switch (Kind) {
                case FileSystemEntryKind.Directory:
                    Info = new DirectoryInfo(Path);
                    break;
                default:
                    Info = new FileInfo(Path);
                    break;
            }
        }
    }
}
