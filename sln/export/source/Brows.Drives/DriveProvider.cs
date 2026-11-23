using Brows.FileSystem;
using Brows.Sys;
using Brows.Sys.Messages.DeviceMessages;
using Brows.Sys.Messages.DeviceMessages.DeviceChanges;
using Brows.Sys.Messages.DeviceMessages.Devices;
using Domore.Logs;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows;

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

    protected sealed override async Task<bool> Take(ISystemMessage message, CancellationToken token) {
        if (message is DeviceChange deviceChange) {
            if (deviceChange.Device is VolumeDevice volume) {
                var drives = volume.VolumeName?.Trim() ?? "";
                if (drives != "") {


                    if (deviceChange is DeviceArrival) {
                        var info = default(DriveInfo);
                        try {
                            info = new DriveInfo($"{drives}");
                        }
                        catch (Exception ex) {
                            if (Log.Error()) {
                                Log.Error(ex);
                            }
                        }
                        if (info != null) {
                            await Provide(new DriveEntry(this, info), token);
                        }
                    }
                    if (deviceChange is DeviceRemovalComplete) {
                        if (drives.Length == 1) {
                            var entry = Provided.FirstOrDefault(e => e.Char == drives[0]);
                            if (entry != null) {
                                await Revoke(entry, token);
                            }
                        }
                    }
                }
            }
        }
        return false;
    }

    public string Name =>
        Factory.Translation?.Value(ID);

    public object Icon { get; }
    public DriveProviderFactory Factory { get; }

    public DriveProvider(DriveProviderFactory factory, string id, object icon) : base(id) {
        Icon = icon;
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }
}
