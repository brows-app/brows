using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class __Log : Command, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new InputTrigger("__log");
            }
        }

        protected override async IAsyncEnumerable<ICommandSuggestion> ProtectedSuggestAsync(ICommandContext context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            await Task.CompletedTask;
            yield break;
        }

        protected override async Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasCommander(out var commander)) {
                await commander.ShowLog(cancellationToken);
                return true;
            }
            return false;
        }
    }
}
