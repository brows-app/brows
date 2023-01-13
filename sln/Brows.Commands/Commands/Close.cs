using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class Close : Command, ICommandExport {
        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
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
