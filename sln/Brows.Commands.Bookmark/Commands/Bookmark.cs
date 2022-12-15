using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Data;
    using Triggers;

    internal class Bookmark : Command<Bookmark.Parameter>, ICommandExport {
        private readonly Dictionary<string, KeyboardGesture> Key = new Dictionary<string, KeyboardGesture> {
            { nameof(View), new KeyboardGesture(KeyboardKey.B, KeyboardModifiers.Control) },
            { nameof(Add), new KeyboardGesture(KeyboardKey.D, KeyboardModifiers.Control) },
            { nameof(AddAll), new KeyboardGesture(KeyboardKey.D, KeyboardModifiers.Control | KeyboardModifiers.Shift) } };

        private DataManager<BookmarkCollection> Data =>
            _Data ?? (
            _Data = new DataManager<BookmarkCollection>());
        private DataManager<BookmarkCollection> _Data;

        private async Task<bool> Add(ICommandContext context, IEnumerable<string> values, CancellationToken cancellationToken) {
            if (values == null) return false;
            if (context == null) return false;
            if (context.CanBookmark(out var bookmark)) {
                foreach (var value in values) {
                    if (value != null) {
                        var collection = await Data.Load(cancellationToken);
                        var items = collection.Items;
                        var existing = items
                            .Select(item => new KeyValuePair<string, string>(item.Key, item.Value))
                            .ToList();
                        var newItem = await bookmark.MakeFrom(value, existing, cancellationToken);
                        collection.Add(newItem);
                    }
                }
                return true;
            }
            return false;
        }

        private async Task<bool> AddActive(Context context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasPanel(out var active)) {
                return await Add(context, new[] { active.ID?.Value }, cancellationToken);
            }
            return false;
        }

        private async Task<bool> AddAll(Context context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasPanels(out var all)) {
                return await Add(context, all.Select(panel => panel.ID?.Value), cancellationToken);
            }
            return false;
        }

        private async Task<bool> View(Context context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasCommander(out var commander)) {
                await commander.ShowPalette(InputTrigger(), cancellationToken);
                return true;
            }
            return false;
        }

        private async Task<bool> Work(Context context, KeyboardGesture key, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (key.Equals(Key[nameof(Add)])) {
                if (context.HasPanel(out var active)) {
                    return await Add(context, new[] { active.ID?.Value }, cancellationToken);
                }
            }
            if (key.Equals(Key[nameof(AddAll)])) {
                return await AddAll(context, cancellationToken);
            }
            if (key.Equals(Key[nameof(View)])) {
                return await View(context, cancellationToken);
            }
            return false;
        }

        private async Task<bool> Work(Context context, Parameter parameter, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (parameter == null) return false;
            if (parameter.All) {
                return await AddAll(context, cancellationToken);
            }
            if (parameter.Add) {
                return await AddActive(context, cancellationToken);
            }
            return false;
        }

        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(Key[nameof(View)]);
                yield return new KeyboardTrigger(Key[nameof(Add)]);
                yield return new KeyboardTrigger(Key[nameof(AddAll)]);
                yield return new InputTrigger("bookmark", "bm");
            }
        }

        protected override async IAsyncEnumerable<ICommandSuggestion> SuggestAsync(Context context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (context != null) {
                if (context.DidTrigger(this)) {
                    if (context.HasData(out _) == false) {
                        var collection = await Data.Load(cancellationToken);
                        if (collection.Items.Any()) {
                            var data = new BookmarkData(this, context, collection);
                            context.SetData(data);
                            context.SetHint(data);
                        }
                    }
                }
            }
            await foreach (var suggestion in base.SuggestAsync(context, cancellationToken)) {
                yield return suggestion;
            }
        }

        protected override async Task<bool> WorkAsync(Context context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasKey(out var key)) {
                return await Work(context, key, cancellationToken);
            }
            if (context.HasParameter(out var parameter)) {
                return await Work(context, parameter, cancellationToken);
            }
            return false;
        }

        public class Parameter {
            [Switch(Name = "add", ShortName = 'a')]
            public bool Add { get; set; }

            [Switch(Name = "all")]
            public bool All { get; set; }
        }
    }
}
