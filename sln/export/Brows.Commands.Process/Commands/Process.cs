using Brows.Exports;
using Brows.Extensions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Brows.Commands {
    internal sealed class Process : Command {
        private readonly CommandProcessHistory History = new CommandProcessHistory();

        private CommandProcess CommandProcess =>
            _CommandProcess ?? (
            _CommandProcess = new CommandProcess { Fix = Fix });
        private CommandProcess _CommandProcess;

        private async IAsyncEnumerable<ICommandSuggestion> SuggestHistory(ICommandContext context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (context == null) yield break;
            if (context.DidTrigger(this)) {
                var list = CommandProcess.List;
                var data = list.Count > 0 ? new CommandProcessData(this, 0, list) : null;
                context.SetData(data);
                context.SetHint(data);

                if (context.HasLine(out var line)) {
                    if (line.HasParameter(out var parameter)) {
                        var history = History.Get(cancellationToken);
                        var inputTrigger = InputTrigger();
                        await foreach (var item in history) {
                            var relevance = SuggestionRelevance.From(item, parameter);
                            if (relevance.HasValue) {
                                yield return Suggestion(
                                    context: context,
                                    description: "",
                                    group: nameof(Process),
                                    input: $"{inputTrigger} {item}",
                                    press: "",
                                    relevance: relevance.Value);
                            }
                        }
                    }
                }
            }
        }

        protected sealed override async IAsyncEnumerable<ICommandSuggestion> Suggest(ICommandContext context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            await foreach (var suggestion in SuggestHistory(context, cancellationToken)) {
                yield return suggestion;
            }
            await foreach (var suggestion in base.Suggest(context, cancellationToken)) {
                yield return suggestion;
            }
        }

        protected sealed override bool TriggeredWork(ICommandContext context) {
            if (context == null) return false;
            if (context.HasCommander(out var commander) == false) {
                return false;
            }
            var inputTrigger = InputTrigger();
            var contextgesture = default(IGesture);
            if (context.HasGesture(out var gesture)) {
                contextgesture = gesture;
            }
            var contextParam = default(string);
            if (context.HasLine(out var line) && line.HasParameter(out var param)) {
                contextParam = param;
                context.SetFlag(new CommandContextFlag {
                    PersistInput = true,
                    RefreshInput = true,
                    SetInput = $"{inputTrigger} {contextParam}",
                    SelectInputLength = contextParam.Length,
                    SelectInputStart = inputTrigger.Length + 1
                });
            }
            return context.Operate(async (progress, token) => {
                if (contextgesture != null) {
                    return await commander.ShowPalette(inputTrigger + " ", token);
                }
                var process = CommandProcess;
                var workingDirectory = await context.WorkingDirectory(token);
                if (contextParam != null) {
                    await History.Add(contextParam, token);
                    await process.Start(contextParam, workingDirectory, token);
                    return true;
                }
                await process.Start("", workingDirectory, token);
                return true;
            });
        }

        public IFixProcessError Fix { get; set; }
    }
}
