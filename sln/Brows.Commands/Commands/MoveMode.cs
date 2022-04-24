using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal abstract class MoveMode : Command<MoveMode.Info> {
        protected abstract PanelPassiveMode Mode { get; }

        protected override async Task<bool> ProtectedWorkAsync(Context context, CancellationToken cancellationToken) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.HasPanels(Mode, out var active, out var passive)) {
                var entries = active.Selection();
                await passive.Deploy(moveEntries: entries, cancellationToken: cancellationToken);
                return true;
            }
            return false;
        }

        public class Info {
        }
    }
}
