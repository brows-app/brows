using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class DriveProvider : EntryProvider<DriveEntry> {
        protected abstract DriveEntry Create(DriveInfo info, CancellationToken cancellationToken);

        protected override async Task Begin(CancellationToken cancellationToken) {
            var drives = await Task.Run(DriveInfo.GetDrives, cancellationToken);
            var entries = drives.Select(d => Create(d, cancellationToken)).ToList();
            await Provide(entries, cancellationToken);
        }

        protected override async Task Refresh(CancellationToken cancellationToken) {
            var drives = Provided.All.ToList();
            await Revoke(drives, cancellationToken);
            await Begin(cancellationToken);
        }

        public static string DriveProviderID { get; } = typeof(DriveProvider).ToString();

        public override IPanelID PanelID =>
            _PanelID ?? (
            _PanelID = new DrivePanelID());
        private IPanelID _PanelID;

        public override IReadOnlySet<string> DataKeyOptions => DriveEntryData.Options;
        public override IReadOnlySet<string> DataKeyDefault => DriveEntryData.Defaults;

        public override IReadOnlyDictionary<string, IEntryColumn> DataKeyColumns =>
            DriveEntryData.Columns;

        public override IOperator Operator(IOperatorDeployment deployment) {
            return new DriveOperator(deployment);
        }
    }
}
