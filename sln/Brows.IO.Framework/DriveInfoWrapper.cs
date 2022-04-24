using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Logger;
    using Threading.Tasks;

    internal class DriveInfoWrapper {
        private readonly ILog Log = Logging.For(typeof(DriveInfoWrapper));
        private readonly DriveInfo Info;

        private long? AvailableFreeSpace;
        private string DriveFormat;
        private DriveType? DriveType;
        private bool? IsReady;
        private string Name;
        private bool Refreshed;
        private bool Refreshing;
        private DirectoryInfo RootDirectory;
        private long? TotalFreeSpace;
        private long? TotalSize;
        private string VolumeLabel;

        private readonly CancellationToken CancellationToken;
        private readonly object Locker = new object();

        private static DriveInfoWrapper Default { get; } = new DriveInfoWrapper(null, default);

        private static IDictionary<string, IEntryData> DefaultData =>
            _DefaultData ?? (
            _DefaultData = Default.Data());
        private static IDictionary<string, IEntryData> _DefaultData;

        private DriveInfoWrapper(DriveInfo info, CancellationToken cancellationToken) {
            Info = info;
            CancellationToken = cancellationToken;
        }

        private async Task<object> Get(Func<DriveInfoWrapper, object> func) {
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
                                DriveType = info?.DriveType;
                                Name = info?.Name;
                                RootDirectory = info?.RootDirectory;

                                var ready = IsReady = info?.IsReady;
                                if (ready == true) {
                                    AvailableFreeSpace = info?.AvailableFreeSpace;
                                    DriveFormat = info?.DriveFormat;
                                    TotalFreeSpace = info?.TotalFreeSpace;
                                    TotalSize = info?.TotalSize;
                                    VolumeLabel = info?.VolumeLabel;
                                }
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
                { nameof(AvailableFreeSpace), EntryDataConverter.FileSystemSize },
                { nameof(TotalFreeSpace), EntryDataConverter.FileSystemSize },
                { nameof(TotalSize), EntryDataConverter.FileSystemSize },
            });
        private static IReadOnlyDictionary<string, IEntryDataConverter> _Converters;


        public IDictionary<string, IEntryData> Data() {
            return new Dictionary<string, IEntryData> {
                { nameof(AvailableFreeSpace), new DataItem(nameof(AvailableFreeSpace), this, i => i.AvailableFreeSpace) },
                { nameof(DriveFormat), new DataItem(nameof(DriveFormat), this, i => i.DriveFormat) },
                { nameof(DriveType), new DataItem(nameof(DriveType), this, i => i.DriveType) },
                { nameof(IsReady), new DataItem(nameof(IsReady), this, i => i.IsReady) },
                { nameof(Name), new DataItem(nameof(Name), this, i => i.Name) },
                { nameof(RootDirectory), new DataItem(nameof(RootDirectory), this, i => i.RootDirectory) },
                { nameof(TotalFreeSpace), new DataItem(nameof(TotalFreeSpace), this, i => i.TotalFreeSpace) },
                { nameof(TotalSize), new DataItem(nameof(TotalSize), this, i => i.TotalSize) },
                { nameof(VolumeLabel), new DataItem(nameof(VolumeLabel), this, i => i.VolumeLabel) }
            };
        }

        public static DriveInfoWrapper For(DriveInfo info, CancellationToken cancellationToken) {
            return new DriveInfoWrapper(info, cancellationToken);
        }

        private sealed class DataItem : EntryData {
            protected sealed override Task<object> Access() {
                return Wrap.Get(Func);
            }

            public string Name { get; }
            public DriveInfoWrapper Wrap { get; }
            public Func<DriveInfoWrapper, object> Func { get; }

            public DataItem(string name, DriveInfoWrapper wrap, Func<DriveInfoWrapper, object> func) : base(name) {
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
