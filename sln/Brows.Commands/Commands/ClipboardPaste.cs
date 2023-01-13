using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class ClipboardPaste : Command, ICommandExport {
        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasClipboard(out var clipboard)) {
                if (context.HasPanel(out var active)) {
                    var files = clipboard.GetFileDropList();
                    active.Deploy(copyFiles: files);
                    return await Worked;
                }
            }
            return false;
        }
    }
}
