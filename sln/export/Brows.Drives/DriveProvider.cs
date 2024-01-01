using Brows.FileSystem;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class DriveProvider : Provider<DriveEntry, DrivesConfig>, IFileSystemNavigationProvider {
        private async Task<IReadOnlyList<DriveEntry>> List(CancellationToken token) {
            return await Task.Run(cancellationToken: token, function: () => {
                var drives = DriveInfo.GetDrives();
                var entries = drives.Select(d => new DriveEntry(this, d)).ToList();
                return entries;
            });
        }

        protected sealed override async Task Begin(CancellationToken token) {
            await Provide(await List(token));
        }

        protected sealed override async Task Refresh(CancellationToken token) {
            await Revoke(Provided);
            await Provide(await List(token));
        }

        public string Name =>
            Translation.Global.Value(ID);

        public object Icon { get; }
        public DriveProviderFactory Factory { get; }

        public DriveProvider(DriveProviderFactory factory, string id, object icon) : base(id) {
            Icon = icon;
            Factory = factory;
        }
    }
}
