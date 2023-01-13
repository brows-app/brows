using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class RefreshAll : Command, ICommandExport {
        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasPanels(out var collection)) {
                foreach (var panel in collection) {
                    panel.Refresh();
                }
                return await Worked;
            }
            return false;
        }
    }
}
