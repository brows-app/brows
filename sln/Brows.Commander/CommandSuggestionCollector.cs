using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class CommandSuggestionCollector {
        private CommandSuggester Suggester =>
            _Suggester ?? (
            _Suggester = new CommandSuggester(Context, Commands));
        private CommandSuggester _Suggester;

        public event EventHandler InputSuggestionChanged;

        public CommandSuggestionCollection Collection { get; set; }
        public int InputSuggestionRelevance { get; private set; } = int.MinValue;
        public string InputSuggestion { get; private set; }

        public ICommandContext Context { get; }
        public IEnumerable<ICommand> Commands { get; }

        public CommandSuggestionCollector(ICommandContext context, IEnumerable<ICommand> commands) {
            Context = context;
            Commands = commands;
        }

        public async Task Collect(CancellationToken token) {
            await foreach (var suggestion in Suggester.Suggest(token)) {
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                var collection = Collection;
                if (collection != null) {
                    collection.Add(suggestion);
                }
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
    }
}
