using Brows.Exports;
using Brows.FileSystem;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class DriveProviderFactory : ProviderFactory<DriveProvider>, IFileSystemNavigationService {
        protected sealed override async Task<DriveProvider> CreateFor(string id, IPanel panel, CancellationToken token) {
            if (panel is null) throw new ArgumentNullException(nameof(panel));
            if (id != Drives.ID) {
                return null;
            }
            var drives = new Drives();
            var icon = default(object);
            var task = DrivesIcon?.Work(drives, set: result => icon = result, token);
            if (task != null) {
                var work = await task;
                if (work == false) {
                    icon = null;
                }
            }
            return new DriveProvider(this, Drives.ID, icon);
        }

        public IDriveIcon DriveIcon { get; set; }
        public IDrivesIcon DrivesIcon { get; set; }
        public IFileSystemIcon FileSystemIcon { get; set; }
    }
}
