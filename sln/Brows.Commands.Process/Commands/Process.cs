using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Extensions;
    using Triggers;

    internal sealed class Process : Command, ICommandExport {
        private readonly CommandProcessHistory History = new CommandProcessHistory();

        private CommandProcess CommandProcess =>
            _CommandProcess ?? (
            _CommandProcess = new CommandProcess());
        private CommandProcess _CommandProcess;

        private async IAsyncEnumerable<ICommandSuggestion> SuggestHistory(ICommandContext context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (context == null) yield break;
            if (context.DidTrigger(this)) {
                var list = CommandProcess.List;
                var data = list.Count > 0 ? new CommandProcessData(this, 0, list) : null;
                context.SetData(data);
                context.SetHint(data);

                if (context.HasInfo(out var info)) {
                    var process = info.Parameter?.Trim() ?? "";
                    if (process != "") {
                        var history = History.Get(cancellationToken);
                        var inputTrigger = InputTrigger();
                        await foreach (var item in history) {
                            var relevance = SuggestionRelevance.From(item, process);
                            if (relevance.HasValue) {
                                yield return new CommandSuggestion(this, context) {
                                    Group = nameof(Process),
                                    Input = $"{inputTrigger} {item}",
                                    Relevance = relevance.Value,
                                    Description = ""
                                };
                            }
                        }
                    }
                }
            }
        }

        protected sealed override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new InputTrigger(">");
                yield return new KeyboardTrigger(KeyboardKey.OemPeriod, KeyboardModifiers.Shift);
            }
        }

        protected sealed override async IAsyncEnumerable<ICommandSuggestion> ProtectedSuggestAsync(ICommandContext context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            await foreach (var suggestion in SuggestHistory(context, cancellationToken)) {
                yield return suggestion;
            }
            await foreach (var suggestion in base.ProtectedSuggestAsync(context, cancellationToken)) {
                yield return suggestion;
            }
        }

        protected sealed override async Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasKey(out _)) {
                if (context.HasCommander(out var commander)) {
                    await commander.ShowPalette("> ", cancellationToken);
                    return true;
                }
                return false;
            }
            var process = CommandProcess;
            var workingDirectory = await context.WorkingDirectory(cancellationToken);
            if (context.HasInfo(out var info)) {
                var p = info.Parameter;
                var t = InputTrigger();
                context.SetFlag(new CommandContextFlag {
                    PersistInput = true,
                    RefreshInput = true,
                    SetInput = $"{t} {p}",
                    SelectInputLength = p.Length,
                    SelectInputStart = t.Length + 1
                });
                await History.Add(p, cancellationToken);
                await process.Start(p, workingDirectory, cancellationToken);
            }
            else {
                await process.Start("", workingDirectory, cancellationToken);
            }
            return true;
        }
    }
}
