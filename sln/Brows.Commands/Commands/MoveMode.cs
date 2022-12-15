using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal abstract class MoveMode : Command<MoveMode.Info> {
        protected abstract PanelPassiveMode Mode { get; }

        protected override async Task<bool> WorkAsync(Context context, CancellationToken cancellationToken) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.HasPanels(Mode, out var active, out var passive)) {
                var entries = active.Selection();
                passive.Deploy(moveEntries: entries);
                return await Completed;
            }
            return false;
        }

        public class Info {
        }
    }
}
