using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class HistoryForward : Command, ICommandExport {
        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context?.HasPanel(out var active) == true) {
                await active.HistoryForward(cancellationToken);
                return true;
            }
            return false;
        }
    }
}
