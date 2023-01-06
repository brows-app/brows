using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class Reset : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.R, KeyboardModifiers.Control | KeyboardModifiers.Alt);
            }
        }

        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasPanel(out var panel)) {
                panel.Reset();
                return await Worked;
            }
            return false;
        }
    }
}
