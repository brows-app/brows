using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class ResetAll : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.R, KeyboardModifiers.Control | KeyboardModifiers.Shift | KeyboardModifiers.Alt);
            }
        }

        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasPanels(out var collection)) {
                foreach (var panel in collection) {
                    panel.Reset();
                }
                return await Worked;
            }
            return false;
        }
    }
}
