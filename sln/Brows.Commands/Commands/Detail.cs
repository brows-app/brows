using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class Detail : Command, ICommandExport {
        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasPanel(out var active)) {
                var entries = active.Selection();
                if (entries != null) {
                    foreach (var entry in entries) {
                        entry.Detail();
                    }
                }
                return await Worked;
            }
            return false;
        }
    }
}
