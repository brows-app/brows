using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class CycleBack : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.Tab, KeyboardModifiers.Shift);
            }
        }

        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasPanels(out var panels)) {
                for (var i = 0; i < panels.Count; i++) {
                    var panel = panels[i];
                    if (panel.Active) {
                        if (i > 0) {
                            panels[i - 1].Activate();
                        }
                        else {
                            panels[panels.Count - 1].Activate();
                        }
                        break;
                    }
                }
                return true;
            }
            await Task.CompletedTask;
            return false;
        }
    }
}
