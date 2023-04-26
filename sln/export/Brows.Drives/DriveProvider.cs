using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class DriveProvider : Provider<DriveEntry, DrivesConfig> {
        private async Task<IReadOnlyList<DriveEntry>> List(CancellationToken token) {
            var drives = await Task.Run(cancellationToken: token, function: DriveInfo.GetDrives);
            var entries = drives.Select(d => new DriveEntry(this, d)).ToList();
            return entries;
        }

        protected sealed override async Task Begin(CancellationToken cancellationToken) {
            await Provide(await List(cancellationToken));
        }

        protected sealed override async Task Refresh(CancellationToken cancellationToken) {
            await Revoke(Provided);
            await Provide(await List(cancellationToken));
        }

        public string Name =>
            Translation.Global.Value(ID);

        public object Icon { get; }

        public DriveProvider(string id, object icon) : base(id) {
            Icon = icon;
        }
    }
}
