using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class CopyHere : CopyBase, ICommandExport {
        protected override async Task<bool> Work(Context context, CancellationToken cancellationToken) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.HasPanel(out var active)) {
                var entries = active.Selection();
                active.Deploy(copyEntries: entries, nativeRenameOnCollision: true);
                return await Worked;
            }
            return false;
        }
    }
}
