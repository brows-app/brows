using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class Save : Command<Save.Info>, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.S, KeyboardModifiers.Control);
            }
        }

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
