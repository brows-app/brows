using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class Save : Command<Save.Info>, ICommandExport {
        protected override async Task<bool> Work(Context context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasPanel(out var active)) {
                active.Save();
                return await Worked;
            }
            return false;
        }

        public class Info {
        }
    }
}
