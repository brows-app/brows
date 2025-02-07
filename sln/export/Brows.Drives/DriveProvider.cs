using Brows.FileSystem;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class DriveProvider : Provider<DriveEntry, DrivesConfig>, IFileSystemNavigationProvider {
        private static readonly ILog Log = Logging.For(typeof(DriveProvider));

        private Task<IReadOnlyList<DriveEntry>> List(CancellationToken token) {
            return Task.Run(cancellationToken: token, function: () => {
                var drives = DriveInfo.GetDrives();
                var entries = drives.Select(d => new DriveEntry(this, d)).ToList();
                return (IReadOnlyList<DriveEntry>)entries;
            });
        }

        protected sealed override async Task Begin(CancellationToken token) {
            await Provide(await List(token), token);
        }

        protected sealed override async Task Refresh(CancellationToken token) {
            await Revoke(Provided, token);
            await Provide(await List(token), token);
        }

        protected sealed override async Task<bool> Take(IMessage message, CancellationToken token) {
            if (message is DeviceChange deviceChange) {
                if (deviceChange.Info is DeviceChangeVolume volume) {
                    var drives = volume.Drive;
                    if (drives != null) {
                        foreach (var drive in drives) {
                            switch (deviceChange.Kind) {
                                case DeviceChangeKind.Arrival:
                                    var info = default(DriveInfo);
                                    try {
                                        info = new DriveInfo($"{drive}");
                                    }
                                    catch (Exception ex) {
                                        if (Log.Error()) {
                                            Log.Error(ex);
                                        }
                                    }
                                    if (info != null) {
                                        await Provide(new DriveEntry(this, info), token);
                                    }
                                    break;
                                case DeviceChangeKind.RemovalComplete:
                                    var entry = Provided.FirstOrDefault(e => e.Char == drive);
                                    if (entry != null) {
                                        await Revoke(entry, token);
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
            return false;
        }

        public string Name =>
            Translation.Global.Value(ID);

        public object Icon { get; }
        public DriveProviderFactory Factory { get; }

        public DriveProvider(DriveProviderFactory factory, string id, object icon) : base(id) {
            Icon = icon;
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }
    }
}
