using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PROCESS = System.Diagnostics.Process;

namespace Brows.Commands {
    using Triggers;

    internal class Explorer : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.F10, KeyboardModifiers.Alt);
                yield return new InputTrigger("explorer");
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasPanel(out var active)) {
                var location = active.ID?.Value?.Trim() ?? "";
                if (location != "") {
                    using (PROCESS.Start("explorer.exe", location)) {
                    }
                    return true;
                }
            }
            await Task.CompletedTask;
            return false;
        }
    }
}
