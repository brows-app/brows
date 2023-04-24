using Brows.Exports;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Data {
    internal static class FileSystemInfoData {
        public sealed class Attributes : Definition<FileAttributes?> {
            public Attributes() : base(i => i.Attributes) {
                Width = 75;
            }
        }

        public sealed class CreationTime : Definition<DateTime?> {
            public CreationTime() : base(i => i.CreationTime) {
                Width = 175;
                Converter = EntryDataConverter.DateTime;
            }
        }

        public sealed class CreationTimeUtc : Definition<DateTime?> {
            public CreationTimeUtc() : base(i => i.CreationTimeUtc) {
                Width = 175;
                Converter = EntryDataConverter.DateTime;
            }
        }

        public sealed class LastAccessTime : Definition<DateTime?> {
            public LastAccessTime() : base(i => i.LastAccessTime) {
                Width = 175;
                Converter = EntryDataConverter.DateTime;
            }
        }

        public sealed class LastAccessTimeUtc : Definition<DateTime?> {
            public LastAccessTimeUtc() : base(i => i.LastAccessTimeUtc) {
                Width = 175;
                Converter = EntryDataConverter.DateTime;
            }
        }

        public sealed class LastWriteTime : Definition<DateTime?> {
            public LastWriteTime() : base(i => i.LastWriteTime) {
                Width = 175;
                Converter = EntryDataConverter.DateTime;
            }
        }

        public sealed class LastWriteTimeUtc : Definition<DateTime?> {
            public LastWriteTimeUtc() : base(i => i.LastWriteTimeUtc) {
                Width = 175;
                Converter = EntryDataConverter.DateTime;
            }
        }

        public sealed class Length : Definition<long?> {
            public Length() : base(i => i is FileInfo file ? file.Length : null) {
                Width = 75;
                Converter = EntryDataConverter.FileSystemSize;
                Alignment = EntryDataAlignment.Right;
            }
        }

        public sealed class LinkTarget : Definition<string> {
            public LinkTarget() : base(i => i.LinkTarget) {
                Width = 100;
            }
        }

        public sealed class Icon : ImageBase {
            protected sealed override async Task<object> GetSource(FileSystemEntry entry, CancellationToken token) {
                if (entry == null) {
                    return null;
                }
                var icon = default(object);
                var task = Service?.Work(entry.Info, set: result => icon = result, token);
                if (task != null) {
                    await task;
                }
                return icon;
            }

            public IIconFileSystemInfo Service { get; set; }

            public sealed override bool SuggestKey(ICommandContext context) {
                return false;
            }
        }

        public sealed class Overlay : ImageBase {
            protected sealed override async Task<object> GetSource(FileSystemEntry entry, CancellationToken token) {
                if (entry == null) {
                    return null;
                }
                var service = Service;
                if (service == null) {
                    return null;
                }
                var result = default(object);
                var worked = await service.Work(entry.Info, set: overlay => result = overlay, token);
                if (worked == false) {
                    return null;
                }
                return result;
            }

            public IOverlayFileSystemInfo Service { get; set; }

            public sealed override bool SuggestKey(ICommandContext context) {
                return false;
            }
        }

        public sealed class Thumbnail : ImageBase {
            protected sealed override async Task<object> GetSource(FileSystemEntry entry, CancellationToken token) {
                if (entry == null) {
                    return null;
                }
                var obj = default(object);
                var task = Service?.Work(entry.Info, ImageWidth, ImageHeight, set: result => obj = result, token);
                if (task != null) {
                    await task;
                }
                return obj;
            }

            public IThumbnailFileSystemInfo Service { get; set; }

            public Thumbnail() {
                Width = 125;
                ImageWidth = 100;
                ImageHeight = 100;
            }
        }

        public sealed class Image : ImageBase {
            protected sealed override async Task<object> GetSource(FileSystemEntry entry, CancellationToken token) {
                if (entry == null) {
                    return null;
                }
                var obj = default(object);
                var task = Service?.Work(entry.Info, ImageWidth, ImageHeight, set: result => obj = result, token);
                if (task != null) {
                    await task;
                }
                return obj;
            }

            public IImageFileSystemInfo Service { get; set; }

            public Image() {
                ImageWidth = 1280;
                ImageHeight = 960;
            }

            public sealed override bool SuggestKey(ICommandContext context) {
                return false;
            }
        }

        public abstract class ImageBase : FileSystemInfoData<object> {
            protected abstract Task<object> GetSource(FileSystemEntry entry, CancellationToken cancellationToken);

            protected sealed override Task<object> GetValue(FileSystemEntry entry, Action<object> progress, CancellationToken cancellationToken) {
                return GetSource(entry, cancellationToken);
            }

            public int ImageWidth {
                get => _ImageWidth;
                set => Change(ref _ImageWidth, value, nameof(ImageWidth));
            }
            private int _ImageWidth;

            public int ImageHeight {
                get => _ImageHeight;
                set => Change(ref _ImageHeight, value, nameof(ImageHeight));
            }
            private int _ImageHeight;
        }

        public abstract class Definition<T> : FileSystemInfoData<T> {
            protected Func<FileSystemInfo, T> Func { get; }

            protected Definition(Func<FileSystemInfo, T> func) {
                Func = func ?? throw new ArgumentNullException(nameof(func));
            }

            protected sealed override void RefreshValue(FileSystemEntry entry) {
                if (entry != null) {
                    if (entry.FileSystemCache.Result != null) {
                        entry.FileSystemCache.Refresh();
                    }
                }
            }

            protected sealed override async Task<T> GetValue(FileSystemEntry entry, Action<T> progress, CancellationToken token) {
                var info = await entry.FileSystemCache.Ready(token);
                if (info.Exists) {
                    return Func(info);
                }
                return default;
            }
        }
    }

    internal abstract class FileSystemInfoData<T> : EntryDataDefinition<FileSystemEntry, T> {
        public sealed override string Group =>
            nameof(FileSystemInfoData);
    }
}
