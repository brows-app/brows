using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class RemovePanel : Command, ICommandExport {
        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.HasCommander(out var commander)) {
                if (context.HasPanel(out var active)) {
                    await commander.RemovePanel(active, cancellationToken);
                    return true;
                }
            }
            return false;
        }
    }
}
