using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class Open : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.Enter);
                yield return new KeyboardTrigger(KeyboardKey.Down, KeyboardModifiers.Alt);
            }
        }

        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasPanel(out var active)) {
                var entries = active.Selection();
                if (entries != null) {
                    foreach (var entry in entries) {
                        entry.Open();
                    }
                }
                return true;
            }
            await Task.CompletedTask;
            return false;
        }
    }
}
