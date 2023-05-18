using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Brows.Commands {
    internal sealed class Operations : Command<OperationsParameter> {
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
            if (null == context) return false;
            if (false == context.HasCommander(out var commander)) return false;
            if (false == context.HasParameter(out var parameter)) {
                return context.HasGesture(out _) == false
                    ? false
                    : context.ShowPalette($"{InputTrigger} ");
            }
            if (false == commander.HasOperations(out var collection)) return false;
            var clearErrors = parameter.ClearErrors == true;
            if (clearErrors) {
                var errors = collection.AsEnumerable().Where(operation => operation.CompleteWithError);
                var list = errors.ToList();
                foreach (var item in list) {
                    collection.Remove(item);
                }
                return true;
            }
            return false;
        }
    }
}
