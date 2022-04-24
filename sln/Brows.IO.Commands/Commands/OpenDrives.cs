using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class OpenDrives : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new InputTrigger("drives");
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            return await context.OpenOrAddPanel(
                DriveProvider.DriveProviderID,
                cancellationToken);
        }
    }
}
