using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Brows.Commands {
    internal sealed class Operations : Command<Operations.Parameter> {
        protected sealed override async IAsyncEnumerable<ICommandSuggestion> Suggest(Context context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (context == null) yield break;
            if (context.DidTrigger(this)) {
                if (context.HasCommander(out var commander)) {
                    if (commander.HasOperations(out var operations)) {
                        //context.SetData(new OperationData(this, 0, operations));
                    }
                }
            }
            await foreach (var suggestion in base.Suggest(context, cancellationToken)) {
                yield return suggestion;
            }
        }

        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasCommander(out var commander) == false) {
                return false;
            }
            if (commander.HasOperations(out var collection) == false) {
                return false;
            }
            collection.Where(operation => operation.Complete).ToList().ForEach(operation => {
                collection.Remove(operation);
            });
            return true;
        }

        public sealed class Parameter {
        }
    }
}
