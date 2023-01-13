using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class Exit : Command, ICommandExport {
        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context?.HasCommander(out var commander) == true) {
                commander.Exit();
                return true;
            }
            await Task.CompletedTask;
            return false;
        }
    }
}
