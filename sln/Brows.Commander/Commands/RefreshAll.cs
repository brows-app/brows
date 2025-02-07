using System.Linq;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class RefreshAll : Command {
        protected sealed override bool TriggeredWork(ICommandContext context) {
            if (context == null) return false;
            if (context.HasPanels(out var collection) == false) {
                return false;
            }
            if (collection is not PanelCollection panelCollection) {
                return false;
            }
            return context.Operate(async (progress, token) => {
                var tasks = panelCollection.ToList().Select(panel => panel.Refresh(token)).ToArray();
                if (tasks.Length == 0) {
                    return false;
                }
                var refreshed = await Task.WhenAll(tasks).ConfigureAwait(false);
                return refreshed.Any(r => r);
            });
        }
    }
}
