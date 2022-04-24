using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class HistoryForward : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.Right, KeyboardModifiers.Alt);
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) {
            if (context?.HasPanel(out var active) == true) {
                await active.HistoryForward(cancellationToken);
                return true;
            }
            return false;
        }
    }
}
