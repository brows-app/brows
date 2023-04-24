using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class History : Command {
        protected sealed override async IAsyncEnumerable<ICommandSuggestion> Suggest(ICommandContext context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (context == null) yield break;
            if (context.DidTrigger(out _)) yield break;
            if (context.HasInput(out var input) == false) yield break;
            if (context.HasPanels(out var panels) == false) yield break;
            var history = panels.History;
            if (history != null) {
                foreach (var item in history) {
                    var relevance = SuggestionRelevance.From(item, input);
                    if (relevance.HasValue) {
                        yield return Suggestion(
                            context: context,
                            group: "PanelHistory",
                            input: item,
                            relevance: relevance.Value,
                            alias: "",
                            press: "",
                            description: "");
                    }
                    await Task.Yield();
                }
            }
        }
    }
}
