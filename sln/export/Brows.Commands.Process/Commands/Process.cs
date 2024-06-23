using Brows.Exports;
using Brows.Extensions;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Brows.Commands {
    internal sealed class Process : Command {
        private readonly CommandProcessHistory History = new CommandProcessHistory();

        private CommandProcess CommandProcess => _CommandProcess ??=
            new CommandProcess {
                Fix = Fix,
                Environment = new Dictionary<string, string> {
                    { $"BROWS", Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? "0" }
                }
            };
        private CommandProcess _CommandProcess;

        private Dictionary<string, string> Environment(ICommandContext context) {
            var env = new Dictionary<string, string>();
            if (context == null) {
                return env;
            }
            if (context.HasPanels(out var panels)) {
                foreach (var panel in panels) {
                    var panelCol = panel.Column;
                    if (panel.HasProvider(out var provider)) {
                        var varKey = $"BROWS[{panelCol}]";
                        var varVal = provider.ID;
                        env[varKey] = varVal;
                    }
                    //if (panel.HasSelection(out var entries)) {
                    //    var i = 0;
                    //    foreach (var entry in entries) {
                    //        var varKey = $"BROWS[{panelCol}][{i++}]";
                    //        var varVal = entry.ID;
                    //        env[varKey] = varVal;
                    //    }
                    //    env[$"BROWS[{panelCol}].COUNT"] = $"{i}";
                    //}
                }
            }
            return env;
        }

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
                        var inputTrigger = InputTrigger;
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
            var inputTrigger = InputTrigger;
            var processInput = default(string);
            var hasLine = context.HasLine(out var line) && line.HasParameter(out processInput);
            if (hasLine == false) {
                var hasGesture = context.HasGesture(out _);
                if (hasGesture) {
                    return context.ShowPalette(inputTrigger + " ");
                }
            }
            else {
                context.SetFlag(new CommandContextFlag {
                    PersistInput = true,
                    RefreshInput = true,
                    SetInput = $"{inputTrigger} {processInput}",
                    SelectInputLength = processInput.Length,
                    SelectInputStart = inputTrigger.Length + 1
                });
            }
            return context.Operate(async (progress, token) => {
                var env = Environment(context);
                var process = CommandProcess;
                var workingDirectory = await context.WorkingDirectory(token);
                if (processInput != null) {
                    await History.Add(processInput, token);
                    await process.Start(processInput, workingDirectory, env, token);
                    return true;
                }
                await process.Start("", workingDirectory, env, token);
                return true;
            });
        }

        public IFixProcessStartInfoError Fix {
            get => _Fix;
            set {
                if (_Fix != value) {
                    _Fix = value;
                    if (_CommandProcess != null) {
                        _CommandProcess.Fix = _Fix;
                    }
                }
            }
        }
        private IFixProcessStartInfoError _Fix;
    }
}
