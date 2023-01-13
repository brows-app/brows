using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class Preview : Command, ICommandExport {
        protected override Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context.HasPanel(out var active)) {
                active.PreviewMode = active.PreviewMode == PanelPreviewMode.None
                    ? PanelPreviewMode.Default
                    : PanelPreviewMode.None;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }
}
