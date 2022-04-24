using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class Up : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.Up, KeyboardModifiers.Alt);
                yield return new InputTrigger("up");
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) {
            if (context?.HasPanel(out var active) == true) {
                await active.Up(cancellationToken);
                return true;
            }
            return false;
        }
    }
}
