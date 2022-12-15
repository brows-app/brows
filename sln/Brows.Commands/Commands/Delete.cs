using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class Delete : Command<Delete.Info>, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.Delete);
                yield return new KeyboardTrigger(KeyboardKey.Delete, KeyboardModifiers.Shift);
                yield return new InputTrigger("delete");
            }
        }

        protected override async Task<bool> WorkAsync(Context context, CancellationToken cancellationToken) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.HasPanel(out var active)) {
                var unrecoverable = false;
                if (context.HasKey(out var key)) {
                    if (key.Modifiers == KeyboardModifiers.Shift) {
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
                return await Completed;
            }
            return false;
        }

        public class Info {
            [Switch(Name = "unrecoverable", ShortName = 'u')]
            public bool Unrecoverable { get; set; }
        }
    }
}
