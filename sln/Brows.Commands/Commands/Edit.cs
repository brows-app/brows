using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class Edit : Command<Edit.Info>, ICommandExport {
        protected override async Task<bool> Work(Context context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasPanel(out var active)) {
                var selection = active.Selection();
                if (selection.Any()) {
                    foreach (var entry in selection) {
                        entry.Edit();
                    }
                    return await Worked;
                }
            }
            return false;
        }

        public class Info {
        }
    }
}
