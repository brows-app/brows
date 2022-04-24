using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class Palette : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.L, KeyboardModifiers.Control);
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasCommander(out var commander)) {
                await commander.ShowPalette("", cancellationToken);
                return true;
            }
            return false;
        }
    }
}
