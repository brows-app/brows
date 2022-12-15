using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class Preview : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new InputTrigger("preview");
                yield return new KeyboardTrigger(KeyboardKey.P, KeyboardModifiers.Alt);
            }
        }

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
