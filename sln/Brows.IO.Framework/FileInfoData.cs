using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal abstract class FileInfoData {
        private FileInfoData(string key, double width) {
            Key = key;
            Width = width;
        }

        private static Of<T> Create<T>(string key, Func<FileSystemInfoWrapper, T> func, double width, bool? loaded = null, IEntryDataConverter converter = null, EntryDataAlignment? alignment = null) {
            return new Of<T>(key, func, width, loaded ?? false, converter, alignment ?? EntryDataAlignment.Default);
        }

        protected abstract IEntryData Implement(FileSystemInfoWrapper wrap, CancellationToken cancellationToken);

        public string Key { get; }
        public double Width { get; }

        public static readonly IReadOnlyList<FileInfoData> Available = new FileInfoData[] {
            Create(nameof(FileSystemInfoWrapper.Attributes), i => i.Attributes, 75),
            Create(nameof(FileSystemInfoWrapper.CreationTime), i => i.CreationTime, 175, converter: EntryDataConverter.DateTime),
            Create(nameof(FileSystemInfoWrapper.CreationTimeUtc), i => i.CreationTimeUtc, 175, converter: EntryDataConverter.DateTime),
            Create(nameof(FileSystemInfoWrapper.Extension), i => i.Extension, 50),
            Create(nameof(FileSystemInfoWrapper.Kind), i => i.Kind, 75, true),
            Create(nameof(FileSystemInfoWrapper.LastAccessTime), i => i.LastAccessTime, 175, converter: EntryDataConverter.DateTime),
            Create(nameof(FileSystemInfoWrapper.LastAccessTimeUtc), i => i.LastAccessTimeUtc, 175, converter: EntryDataConverter.DateTime),
            Create(nameof(FileSystemInfoWrapper.LastWriteTime), i => i.LastWriteTime, 175, converter: EntryDataConverter.DateTime),
            Create(nameof(FileSystemInfoWrapper.LastWriteTimeUtc), i => i.LastWriteTimeUtc, 175, converter: EntryDataConverter.DateTime),
            Create(nameof(FileSystemInfoWrapper.Length), i => i.Length, 75, converter: EntryDataConverter.FileSystemSize, alignment: EntryDataAlignment.Right),
            Create(nameof(FileSystemInfoWrapper.LinkTarget), i => i.LinkTarget, 100),
            Create(nameof(FileSystemInfoWrapper.Name), i => i.Name, 250, loaded: true)
        };

        public static IReadOnlyList<IEntryData> For(FileSystemInfoWrapper wrap, CancellationToken cancellationToken) {
            return Available.Select(d => d.Implement(wrap, cancellationToken)).ToList();
        }

        private class Of<T> : FileInfoData {
            protected override IEntryData Implement(FileSystemInfoWrapper wrap, CancellationToken cancellationToken) {
                return new Implementation(Key, wrap, Loaded, Func, cancellationToken) {
                    Alignment = Alignment,
                    Converter = Converter

                };
            }

            public bool Loaded { get; }
            public Func<FileSystemInfoWrapper, T> Func { get; }
            public IEntryDataConverter Converter { get; }
            public EntryDataAlignment Alignment { get; }

            public Of(string key, Func<FileSystemInfoWrapper, T> func, double width, bool loaded, IEntryDataConverter converter, EntryDataAlignment alignment) : base(key, width) {
                Func = func;
                Loaded = loaded;
                Converter = converter;
                Alignment = alignment;
            }

            private sealed class Implementation : EntryData<T> {
                public bool Loaded { get; private set; }
                public FileSystemInfoWrapper Wrap { get; }
                public Func<FileSystemInfoWrapper, T> Func { get; }

                public Implementation(string key, FileSystemInfoWrapper wrap, bool loaded, Func<FileSystemInfoWrapper, T> func, CancellationToken cancellationToken) : base(key, cancellationToken) {
                    Func = func;
                    Wrap = wrap;
                    Loaded = loaded;
                }

                protected sealed override async Task<T> Access(CancellationToken cancellationToken) {
                    if (Loaded) {
                        return Func(Wrap);
                    }
                    return await Wrap.Get(Func);
                }

                protected sealed override void Refresh() {
                    Loaded = false;
                    Wrap.RefreshingInfo = true;
                }
            }
        }
    }
}
