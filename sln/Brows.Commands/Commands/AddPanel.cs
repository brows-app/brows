using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class AddPanel : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.N, KeyboardModifiers.Control);
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasCommander(out var commander)) {
                var id = context.HasPanel(out var active)
                    ? active.ID?.Value
                    : "";
                await commander.AddPanel(id ?? "", cancellationToken);
                return true;
            }
            return false;
        }
    }
}
