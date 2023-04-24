using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DRIVETYPE = System.IO.DriveType;

namespace Brows {
    using Exports;

    internal static class DriveInfoData {
        public sealed class Name : DriveInfoData<string> {
            public Name() : base(false, i => i.Name) {
                Width = 250;
            }
        }

        public sealed class DriveType : DriveInfoData<DRIVETYPE> {
            public DriveType() : base(false, i => i.DriveType) {
                Width = 75;
            }
        }

        public sealed class RootDirectory : DriveInfoData<DirectoryInfo> {
            public RootDirectory() : base(false, i => i.RootDirectory) {
                Width = 75;
            }
        }

        public sealed class IsReady : DriveInfoData<bool> {
            public IsReady() : base(false, i => i.IsReady) {
                Width = 75;
            }
        }

        public sealed class AvailableFreeSpace : DriveInfoData<long?> {
            public AvailableFreeSpace() : base(true, i => i.AvailableFreeSpace) {
                Width = 75;
                Converter = EntryDataConverter.FileSystemSize;
            }
        }

        public sealed class DriveFormat : DriveInfoData<string> {
            public DriveFormat() : base(true, i => i.DriveFormat) {
                Width = 75;
            }
        }

        public sealed class TotalFreeSpace : DriveInfoData<long?> {
            public TotalFreeSpace() : base(true, i => i.TotalFreeSpace) {
                Width = 75;
                Converter = EntryDataConverter.FileSystemSize;
            }
        }

        public sealed class TotalSize : DriveInfoData<long?> {
            public TotalSize() : base(true, i => i.TotalSize) {
                Width = 75;
                Converter = EntryDataConverter.FileSystemSize;
            }
        }

        public sealed class VolumeLabel : DriveInfoData<string> {
            public VolumeLabel() : base(true, i => i.VolumeLabel) {
                Width = 100;
            }
        }

        public sealed class Icon : Image {
            protected sealed override async Task<object> GetValue(DriveEntry entry, Action<object> progress, CancellationToken token) {
                if (entry == null) {
                    return null;
                }
                var service = IconDriveInfo;
                if (service == null) {
                    return null;
                }
                var icon = await service.Icon(entry.Info, token);
                return icon;
            }

            public IIconDriveInfo IconDriveInfo { get; set; }

            public sealed override bool SuggestKey(ICommandContext context) {
                return false;
            }
        }

        public abstract class Image : EntryDataDefinition<DriveEntry, object> {
        }
    }

    internal abstract class DriveInfoData<T> : EntryDataDefinition<DriveEntry, T> {
        protected bool Ready { get; }
        protected Func<DriveInfo, T> Func { get; }

        protected DriveInfoData(bool ready, Func<DriveInfo, T> func) {
            Func = func ?? throw new ArgumentNullException(nameof(func));
            Ready = ready;
        }

        protected sealed override void RefreshValue(DriveEntry entry) {
            entry.RefreshComplete = null;
        }

        protected override async Task<T> GetValue(DriveEntry entry, Action<T> progress, CancellationToken cancellationToken) {
            await entry.Refresh(cancellationToken);
            var info = entry.Info;
            if (info.IsReady || Ready == false) {
                return Func(info);
            }
            return default;
        }
    }
}
