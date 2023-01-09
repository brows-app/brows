using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class Edit : Command<Edit.Info>, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.E, KeyboardModifiers.Control);
                yield return new InputTrigger("edit");
            }
        }

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
