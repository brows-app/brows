using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class OpenDrives : Command, ICommandExport {
        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            return await context.OpenOrAddPanel(
                DriveProvider.DriveProviderID,
                cancellationToken);
        }
    }
}
