using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class ClipboardPaste : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.V, KeyboardModifiers.Control);
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasClipboard(out var clipboard)) {
                if (context.HasPanel(out var active)) {
                    var files = clipboard.GetFileDropList();
                    await active.Deploy(copyFiles: files, cancellationToken: cancellationToken);
                    return true;
                }
            }
            return false;
        }
    }
}
