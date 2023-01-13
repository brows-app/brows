using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class Delete : Command<Delete.Info>, ICommandExport {
        protected override async Task<bool> Work(Context context, CancellationToken cancellationToken) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.HasPanel(out var active)) {
                var unrecoverable = false;
                if (context.HasKey(out var key)) {
                    if (key.Modifiers == PressModifiers.Shift) {
                        unrecoverable = true;
                    }
                }
                if (context.HasParameter(out var parameter)) {
                    if (parameter.Unrecoverable) {
                        unrecoverable = true;
                    }
                }
                var selection = active.Selection();
                active.Deploy(
                    nativeTrash: !unrecoverable,
                    deleteEntries: selection);
                return await Worked;
            }
            return false;
        }

        public class Info {
            [Switch(Name = "unrecoverable", ShortName = 'u')]
            public bool Unrecoverable { get; set; }
        }
    }
}
