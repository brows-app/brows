using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Brows.Commands {
    internal sealed class Manual : Command {
        protected sealed override async IAsyncEnumerable<ICommandSuggestion> Suggest(ICommandContext context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (context == null) yield break;
            if (context.DidTrigger(this)) {
                var data = new CommanderManualData(this);
                context.SetData(data);
                context.SetHint(data);
            }
            await foreach (var suggestion in base.Suggest(context, cancellationToken)) {
                yield return suggestion;
            }
        }

        protected sealed override bool TriggeredWork(ICommandContext context) {
            context.SetFlag(new CommandContextFlag {
                PersistInput = true,
                RefreshInput = true,
                SetInput = ""
            });
            return true;
        }
    }
}
