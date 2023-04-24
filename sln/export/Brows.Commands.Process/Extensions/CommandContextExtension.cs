using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Extensions {
    internal static class CommandContextExtension {
        public static async Task<string> WorkingDirectory(this ICommandContext commandContext, CancellationToken cancellationToken) {
            if (null == commandContext) throw new ArgumentNullException(nameof(commandContext));
            return commandContext.HasPanel(out var active)
                ? await active.WorkingDirectory(cancellationToken)
                : null;
        }
    }
}
