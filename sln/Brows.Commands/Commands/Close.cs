using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class Close : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.F4, KeyboardModifiers.Alt);
                yield return new InputTrigger("close");
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasCommander(out var commander)) {
                commander.Close();
                return true;
            }
            await Task.CompletedTask;
            return false;
        }
    }
}
