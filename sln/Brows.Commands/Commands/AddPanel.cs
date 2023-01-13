using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class AddPanel : Command, ICommandExport {
        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasCommander(out var commander)) {
                var id = context.HasPanel(out var active)
                    ? active.ID?.Value
                    : "";
                await commander.AddPanel(id ?? "", cancellationToken);
                return true;
            }
            return false;
        }
    }
}
