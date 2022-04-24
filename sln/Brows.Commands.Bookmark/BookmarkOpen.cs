using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Data;

    internal class BookmarkOpen : Command, ICommandExport {
        private DataManager<BookmarkCollectionModel> Data =>
            _Data ?? (
            _Data = new DataManager<BookmarkCollectionModel>());
        private DataManager<BookmarkCollectionModel> _Data;

        protected override async IAsyncEnumerable<ICommandSuggestion> ProtectedSuggestAsync(ICommandContext context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            var model = await Data.Load(cancellationToken);
            if (context.HasInput(out var input) == true) {
                foreach (var item in model.Items) {
                    var key = item.Key;
                    var keyRelevance = SuggestionRelevance.From(key, input);
                    if (keyRelevance.HasValue) {
                        yield return new CommandSuggestion(this, context) {
                            Description = item.Value,
                            Group = nameof(BookmarkOpen),
                            Input = key,
                            Relevance = keyRelevance.Value
                        };
                    }
                    else {
                        var value = item.Value;
                        var valueRelevance = SuggestionRelevance.From(value, input);
                        if (valueRelevance.HasValue) {
                            yield return new CommandSuggestion(this, context) {
                                Description = key,
                                Group = nameof(BookmarkOpen),
                                Input = value,
                                Relevance = valueRelevance.Value
                            };
                        }
                    }
                    await Task.Yield();
                }
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasInput(out var input)) {
                var model = await Data.Load(cancellationToken);
                var items = model.Items;
                foreach (var item in items) {
                    if (input.Equals(item.Key, StringComparison.CurrentCultureIgnoreCase)) {
                        if (context.HasPanel(out var panel)) {
                            await panel.Open(item.Value, cancellationToken);
                            return true;
                        }
                        if (context.HasCommander(out var commander)) {
                            await commander.AddPanel(item.Value, cancellationToken);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public override bool Arbitrary => true;
    }
}
