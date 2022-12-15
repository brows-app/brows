using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class Refresh : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.R, KeyboardModifiers.Control);
            }
        }

        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context?.HasPanel(out var panel) == true) {
                await panel.Refresh(cancellationToken);
                return true;
            }
            return false;
        }
    }
}
