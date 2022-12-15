using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Data;

    internal class BookmarkOpen : Command, ICommandExport {
        private DataManager<BookmarkCollection> Data =>
            _Data ?? (
            _Data = new DataManager<BookmarkCollection>());
        private DataManager<BookmarkCollection> _Data;

        protected override async IAsyncEnumerable<ICommandSuggestion> Suggest(ICommandContext context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            var model = await Data.Load(cancellationToken);
            if (context.HasInput(out var input) == true) {
                foreach (var item in model.Items) {
                    var key = item.Key;
                    var keyRelevance = SuggestionRelevance.From(key, input);
                    if (keyRelevance.HasValue) {
                        yield return Suggestion(
                            context: context,
                            description: item.Value,
                            group: nameof(BookmarkOpen),
                            input: key,
                            relevance: keyRelevance.Value);
                    }
                    else {
                        var value = item.Value;
                        var valueRelevance = SuggestionRelevance.From(value, input);
                        if (valueRelevance.HasValue) {
                            yield return Suggestion(
                                context: context,
                                description: key,
                                group: nameof(BookmarkOpen),
                                input: value,
                                relevance: valueRelevance.Value);
                        }
                    }
                    await Task.Yield();
                }
            }
        }

        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasInput(out var input)) {
                var model = await Data.Load(cancellationToken);
                var items = model.Items;
                foreach (var item in items) {
                    if (input.Equals(item.Key, StringComparison.CurrentCultureIgnoreCase)) {
                        return await context.OpenOrAddPanel(item.Value, cancellationToken);
                    }
                }
            }
            return false;
        }

        public override bool Arbitrary => true;
    }
}
