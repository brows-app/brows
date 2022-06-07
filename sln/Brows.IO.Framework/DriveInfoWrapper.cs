using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Logger;
    using Threading.Tasks;
    using Align = EntryDataAlignment;

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

        private static IReadOnlyDictionary<string, IEntryData> DefaultData =>
            _DefaultData ?? (
            _DefaultData = Default.Data());
        private static IReadOnlyDictionary<string, IEntryData> _DefaultData;

        private DriveInfoWrapper(DriveInfo info, CancellationToken cancellationToken) {
            Info = info;
            CancellationToken = cancellationToken;
        }

        private static IEnumerable<(IEntryData Data, double Width)> ColumnTable(DriveInfoWrapper item) {
            return new List<(IEntryData, double)> {
                (new DataItem(nameof(AvailableFreeSpace), item, i => i.AvailableFreeSpace) { Alignment = Align.Right, Converter = EntryDataConverter.FileSystemSize },
                    75),
                (new DataItem(nameof(DriveFormat), item, i => i.DriveFormat),
                    75),
                (new DataItem(nameof(DriveType), item, i => i.DriveType),
                    75),
                (new DataItem(nameof(IsReady), item, i => i.IsReady) { Converter = EntryDataConverter.BooleanYesNo },
                    75),
                (new DataItem(nameof(Name), item, i => i.Name),
                    250),
                (new DataItem(nameof(RootDirectory), item, i => i.RootDirectory),
                    75),
                (new DataItem(nameof(TotalFreeSpace), item, i => i.TotalFreeSpace) { Alignment = Align.Right, Converter = EntryDataConverter.FileSystemSize },
                    75),
                (new DataItem(nameof(TotalSize), item, i => i.TotalSize) { Alignment = Align.Right, Converter = EntryDataConverter.FileSystemSize },
                    75),
                (new DataItem(nameof(VolumeLabel), item, i => i.VolumeLabel),
                    100)
            };
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

        public static IReadOnlyDictionary<string, IEntryColumn> Columns =>
            _Columns ?? (
            _Columns = ColumnTable(Default).ToDictionary(
                c => c.Data.Key,
                c => (IEntryColumn)new EntryColumn {
                    Resolver = DriveEntryData.Resolver,
                    Width = c.Width
                }));
        private static IReadOnlyDictionary<string, IEntryColumn> _Columns;

        public IReadOnlyDictionary<string, IEntryData> Data() {
            return ColumnTable(this).ToDictionary(
                c => c.Data.Key,
                c => c.Data);
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
                Wrap = wrap ?? throw new ArgumentNullException(nameof(wrap));
                Name = name;
                Func = func;
            }

            public sealed override void Refresh() {
                Wrap.Refreshed = false;
                base.Refresh();
            }
        }
    }
}
