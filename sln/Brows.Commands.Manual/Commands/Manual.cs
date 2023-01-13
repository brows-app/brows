using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class Manual : Command, ICommandExport {
        protected override async IAsyncEnumerable<ICommandSuggestion> Suggest(ICommandContext context, [EnumeratorCancellation] CancellationToken cancellationToken) {
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

        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            context.SetFlag(new CommandContextFlag {
                PersistInput = true,
                RefreshInput = true,
                SetInput = ""
            });
            await Task.CompletedTask;
            return true;
        }
    }
}
