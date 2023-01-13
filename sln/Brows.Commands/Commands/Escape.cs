using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class Escape : Command, ICommandExport {
        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasPanel(out var active)) {
                active.Input(null);
                return true;
            }
            await Task.CompletedTask;
            return false;
        }
    }
}
