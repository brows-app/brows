using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Brows.Commands {
    using System.Threading.Tasks;

    internal class Operations : Command<Operations.Parameter>, ICommandExport {
        protected override async IAsyncEnumerable<ICommandSuggestion> Suggest(Context context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (context == null) yield break;
            if (context.DidTrigger(this)) {
                if (context.HasCommander(out var commander)) {
                    var operations = commander.Operations;
                    if (operations != null && operations.Count > 0) {
                        context.SetData(new OperationData(this, 0, operations));
                    }
                }
            }
            await foreach (var suggestion in base.Suggest(context, cancellationToken)) {
                yield return suggestion;
            }
        }

        protected override async Task<bool> Work(Context context, CancellationToken cancellationToken) {
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
