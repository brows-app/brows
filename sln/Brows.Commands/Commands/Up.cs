using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class Up : Command, ICommandExport {
        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context?.HasPanel(out var active) == true) {
                await active.OpenParent(cancellationToken);
                return true;
            }
            return false;
        }
    }
}
