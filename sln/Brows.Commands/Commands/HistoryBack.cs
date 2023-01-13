using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class HistoryBack : Command, ICommandExport {
        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context?.HasPanel(out var active) == true) {
                await active.HistoryBack(cancellationToken);
                return true;
            }
            return false;
        }
    }
}
