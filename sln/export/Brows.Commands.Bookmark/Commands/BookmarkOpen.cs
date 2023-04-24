using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Config;

    internal class BookmarkOpen : Command {
        private IConfig<Bookmarks> Data =>
            _Data ?? (
            _Data = Configure.Data<Bookmarks>());
        private IConfig<Bookmarks> _Data;

        protected override async IAsyncEnumerable<ICommandSuggestion> Suggest(ICommandContext context, [EnumeratorCancellation] CancellationToken token) {
            if (context == null) yield break;
            if (context.DidTrigger(out _)) yield break;
            if (context.HasInput(out var input) == false) {
                yield break;
            }
            var data = await Data.Load(token);
            foreach (var item in data.Bookmark) {
                string description() => item.Loc ?? string.Join(" | ", item.Ses);
                var key = item.Key;
                var keyRelevance = SuggestionRelevance.From(key, input);
                if (keyRelevance.HasValue) {
                    yield return Suggestion(
                        context: context,
                        description: description(),
                        group: nameof(BookmarkOpen),
                        input: key,
                        relevance: keyRelevance.Value);
                }
                else {
                    var value = item.Loc;
                    var valueRelevance = SuggestionRelevance.From(item.Loc, input) ?? SuggestionRelevance.From(item.Ses, input);
                    if (valueRelevance.HasValue) {
                        yield return Suggestion(
                            context: context,
                            description: description(),
                            group: nameof(BookmarkOpen),
                            input: key,
                            relevance: valueRelevance.Value);
                    }
                }
            }
        }

        protected sealed override bool ArbitraryWork(ICommandContext context) {
            if (context == null) return false;
            if (context.HasInput(out var input) == false) return false;
            if (context.HasCommander(out var commander) == false) return false;
            var model = Data.Loaded;
            var items = model.Bookmark;
            foreach (var item in items) {
                if (input.Equals(item.Key, StringComparison.CurrentCultureIgnoreCase)) {
                    return context.Operate(async (progress, token) => {
                        return await item.Open(
                            commander,
                            context.HasPanel(out var active) ? active : null,
                            token);
                    });
                }
            }
            return false;
        }

        protected sealed override async Task Init(CancellationToken token) {
            await Data.Load(token);
        }
    }
}
