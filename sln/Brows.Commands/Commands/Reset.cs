using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class Reset : Command, ICommandExport {
        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasPanel(out var panel)) {
                panel.Reset();
                return await Worked;
            }
            return false;
        }
    }
}
