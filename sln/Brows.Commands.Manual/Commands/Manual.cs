using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class Manual : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new InputTrigger("?");
            }
        }

        protected override async IAsyncEnumerable<ICommandSuggestion> ProtectedSuggestAsync(ICommandContext context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (context == null) yield break;
            if (context.DidTrigger(this)) {
                var data = new CommanderManualData(this);
                context.SetData(data);
                context.SetHint(data);
            }
            await foreach (var suggestion in base.ProtectedSuggestAsync(context, cancellationToken)) {
                yield return suggestion;
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) {
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
