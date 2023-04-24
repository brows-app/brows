using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Exports;

    internal sealed class DriveProviderFactory : EntryProviderFactory<DriveProvider> {
        protected sealed override async Task<DriveProvider> CreateFor(string id, IPanel panel, CancellationToken cancellationToken) {
            if (panel is null) throw new ArgumentNullException(nameof(panel));
            if (id != Drives.ID) {
                return null;
            }
            var drives = new Drives();
            var icon = default(object);
            var iconService = IconDrives;
            if (iconService != null) {
                icon = await iconService.Icon(drives, cancellationToken);
            }
            return new DriveProvider(Drives.ID, icon);
        }

        public IIconDrives IconDrives { get; set; }
    }
}
