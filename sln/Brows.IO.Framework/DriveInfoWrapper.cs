using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Threading.Tasks;
    using Align = EntryDataAlignment;

    internal class DriveInfoWrapper {
        private static readonly ILog Log = Logging.For(typeof(DriveInfoWrapper));

        private readonly DriveInfo Info;

        private long? AvailableFreeSpace;
        private string DriveFormat;
        private DriveType? DriveType;
        private bool? IsReady;
        private string Name;
        private Task Refreshed;
        private bool Refreshing;
        private DirectoryInfo RootDirectory;
        private long? TotalFreeSpace;
        private long? TotalSize;
        private string VolumeLabel;

        private readonly CancellationToken CancellationToken;

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
                (DataItem.For(nameof(AvailableFreeSpace), item, i => i.AvailableFreeSpace, alignment: Align.Right, converter: EntryDataConverter.FileSystemSize),
                    75),
                (DataItem.For(nameof(DriveFormat), item, i => i.DriveFormat),
                    75),
                (DataItem.For(nameof(DriveType), item, i => i.DriveType),
                    75),
                (DataItem.For(nameof(IsReady), item, i => i.IsReady, converter: EntryDataConverter.BooleanYesNo),
                    75),
                (DataItem.For(nameof(Name), item, i => i.Name),
                    250),
                (DataItem.For(nameof(RootDirectory), item, i => i.RootDirectory),
                    75),
                (DataItem.For(nameof(TotalFreeSpace), item, i => i.TotalFreeSpace, alignment: Align.Right, converter: EntryDataConverter.FileSystemSize),
                    75),
                (DataItem.For(nameof(TotalSize), item, i => i.TotalSize, alignment: Align.Right, converter: EntryDataConverter.FileSystemSize),
                    75),
                (DataItem.For(nameof(VolumeLabel), item, i => i.VolumeLabel),
                    100)
            };
        }

        private async Task<T> Get<T>(Func<DriveInfoWrapper, T> func) {
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

        private static class DataItem {
            public static DataItem<T> For<T>(string name, DriveInfoWrapper wrap, Func<DriveInfoWrapper, T> func, IEntryDataConverter converter = null, EntryDataAlignment? alignment = null) {
                return new DataItem<T>(name, wrap, func) {
                    Alignment = alignment ?? EntryDataAlignment.Default,
                    Converter = converter
                };
            }
        }

        private sealed class DataItem<T> : EntryData<T> {
            protected sealed override Task<T> Access(CancellationToken cancellationToken) {
                return Wrap.Get(Func);
            }

            protected sealed override void Refresh() {
                Wrap.Refreshed = null;
            }

            public string Name { get; }
            public DriveInfoWrapper Wrap { get; }
            public Func<DriveInfoWrapper, T> Func { get; }

            public DataItem(string name, DriveInfoWrapper wrap, Func<DriveInfoWrapper, T> func) : base(name, wrap?.CancellationToken ?? default) {
                Wrap = wrap ?? throw new ArgumentNullException(nameof(wrap));
                Name = name;
                Func = func;
            }
        }
    }
}
