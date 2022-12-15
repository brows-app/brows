using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class ClipboardCopy : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.C, KeyboardModifiers.Control);
            }
        }

        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.HasClipboard(out var clipboard)) {
                if (context.HasPanel(out var active)) {
                    var selection = active.Selection();
                    var files = selection.Select(entry => entry.File);
                    clipboard.SetFileDropList(files);
                    return true;
                }
            }
            await Task.CompletedTask;
            return false;
        }
    }
}
