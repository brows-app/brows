using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Threading.Tasks;

    internal class CommandSuggestionCollector : IDisposable {
        private bool Ending;
        private readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();

        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<CommandSuggestionCollector>());
        private TaskHandler _TaskHandler;

        private CommandSuggester Suggester =>
            _Suggester ?? (
            _Suggester = new CommandSuggester(Context, Commands));
        private CommandSuggester _Suggester;

        private async Task Collect() {
            await foreach (var suggestion in Suggester.Suggest(CancellationTokenSource.Token)) {
                if (Ending) {
                    break;
                }
                Collection.Add(suggestion);
                if (Context.HasInput(out var input)) {
                    var suggInp = suggestion.Input;
                    if (suggInp.StartsWith(input, StringComparison.CurrentCultureIgnoreCase)) {
                        var relevance = suggestion.Relevance;
                        if (relevance > InputSuggestionRelevance) {
                            InputSuggestionRelevance = relevance;
                            InputSuggestion = suggInp;
                            InputSuggestionChanged?.Invoke(this, EventArgs.Empty);
                        }
                    }
                }
            }
        }

        public event EventHandler InputSuggestionChanged;

        public int InputSuggestionRelevance { get; private set; } = int.MinValue;
        public string InputSuggestion { get; private set; }

        public CommandSuggestionCollection Collection {
            get => _Collection ?? (_Collection = new CommandSuggestionCollection());
            set => _Collection = value;
        }
        private CommandSuggestionCollection _Collection;

        public ICommandContext Context { get; }
        public IEnumerable<ICommand> Commands { get; }

        public CommandSuggestionCollector(ICommandContext context, IEnumerable<ICommand> commands) {
            Context = context;
            Commands = commands;
        }

        public void Begin() {
            TaskHandler.Begin(Collect);
        }

        public void End() {
            Ending = true;
            CancellationTokenSource.Cancel();
        }

        public void Dispose() {
            CancellationTokenSource.Dispose();
        }
    }
}
