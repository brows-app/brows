using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class RemovePanel : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.W, KeyboardModifiers.Control);
                yield return new InputTrigger("rempanel");
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.HasCommander(out var commander)) {
                if (context.HasPanel(out var active)) {
                    await commander.RemovePanel(active, cancellationToken);
                    return true;
                }
            }
            return false;
        }
    }
}
