using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class Save : Command<Save.Info>, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.S, KeyboardModifiers.Control);
                yield return new InputTrigger("save");
            }
        }

        protected override async Task<bool> WorkAsync(Context context, CancellationToken cancellationToken) {
            if (context?.HasPanel(out var active) == true) {
                await active.Save(cancellationToken);
                return true;
            }
            return false;
        }

        public class Info {
        }
    }
}
