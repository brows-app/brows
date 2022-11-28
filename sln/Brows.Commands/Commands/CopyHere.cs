using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class CopyHere : CopyBase, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new InputTrigger("copyhere", "copyh", "ch");
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(Context context, CancellationToken cancellationToken) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.HasPanel(out var active)) {
                var entries = active.Selection();
                await active.Deploy(copyEntries: entries, nativeRenameOnCollision: true, cancellationToken: cancellationToken);
                return true;
            }
            return false;
        }
    }
}
