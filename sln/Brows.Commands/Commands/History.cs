using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class History : Command, ICommandExport {
        protected override async IAsyncEnumerable<ICommandSuggestion> ProtectedSuggestAsync(ICommandContext context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (context == null) yield break;
            if (context.HasInput(out var input)) {
                if (context.HasCommander(out var commander)) {
                    var history = commander.Panels?.History;
                    if (history != null) {
                        foreach (var item in history) {
                            var relevance = SuggestionRelevance.From(item, input);
                            if (relevance.HasValue) {
                                yield return Suggestion(
                                    context: context,
                                    group: nameof(History),
                                    input: item,
                                    relevance: relevance.Value);
                            }
                            await Task.Yield();
                        }
                    }
                }
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasInput(out var input)) {
                if (context.HasCommander(out var commander)) {
                    var history = commander.Panels?.History;
                    if (history != null) {
                        if (history.Contains(input)) {
                            return await context.OpenOrAddPanel(input, cancellationToken);
                        }
                    }
                }
            }
            return false;
        }

        public override bool Arbitrary => true;
    }
}
