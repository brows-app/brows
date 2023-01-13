using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class Explorer : Command, ICommandExport {
        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasPanel(out var active)) {
                var location = active.ID?.Value?.Trim() ?? "";
                if (location != "") {
                    using (Process.Start("explorer.exe", location)) {
                    }
                    return true;
                }
            }
            await Task.CompletedTask;
            return false;
        }
    }
}
