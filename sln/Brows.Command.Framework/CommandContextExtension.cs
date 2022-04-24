using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public static class CommandContextExtension {
        public static async Task<bool> OpenOrAddPanel(this ICommandContext commandContext, string id, CancellationToken cancellationToken) {
            if (commandContext == null) throw new ArgumentNullException(nameof(commandContext));
            if (commandContext.HasPanel(out var active)) {
                await active.Open(id, cancellationToken);
                return true;
            }
            if (commandContext.HasCommander(out var commander)) {
                await commander.AddPanel(id, cancellationToken);
                return true;
            }
            return false;
        }
    }
}
