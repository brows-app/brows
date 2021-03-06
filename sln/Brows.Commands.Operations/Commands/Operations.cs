using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Brows.Commands {
    using System.Threading.Tasks;
    using Triggers;

    internal class Operations : Command<Operations.Parameter>, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new InputTrigger("!");
            }
        }

        protected override async IAsyncEnumerable<ICommandSuggestion> ProtectedSuggestAsync(Context context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (context == null) yield break;
            if (context.DidTrigger(this)) {
                if (context.HasCommander(out var commander)) {
                    var operations = commander.Operations;
                    if (operations != null && operations.Count > 0) {
                        context.SetData(new OperationData(this, 0, operations));
                    }
                }
            }
            await foreach (var suggestion in base.ProtectedSuggestAsync(context, cancellationToken)) {
                yield return suggestion;
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(Context context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasCommander(out var commander)) {
                commander.Operations?.Clear();
                return true;
            }
            await Task.CompletedTask;
            return false;
        }

        public class Parameter {
            [Switch(Name = "clear", ShortName = 'c')]
            public bool Clear { get; set; }
        }
    }
}
