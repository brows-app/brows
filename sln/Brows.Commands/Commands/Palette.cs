using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class Palette : Command, ICommandExport {
        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasCommander(out var commander)) {
                await commander.ShowPalette("", cancellationToken);
                return true;
            }
            return false;
        }
    }
}
