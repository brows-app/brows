using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class Refresh : Command, ICommandExport {
        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasPanel(out var panel)) {
                panel.Refresh();
                return await Worked;
            }
            return false;
        }
    }
}
