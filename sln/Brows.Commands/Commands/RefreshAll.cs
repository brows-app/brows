using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class RefreshAll : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.R, KeyboardModifiers.Control | KeyboardModifiers.Shift);
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) {
            if (context?.HasPanels(out var collection) == true) {
                foreach (var panel in collection) {
                    await panel.Refresh(cancellationToken);
                }
                return true;
            }
            return false;
        }
    }
}
