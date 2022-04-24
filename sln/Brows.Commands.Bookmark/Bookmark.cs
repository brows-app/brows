using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Data;
    using Triggers;

    internal class Bookmark : Command, ICommandExport {
        private DataManager<BookmarkCollectionModel> Data =>
            _Data ?? (
            _Data = new DataManager<BookmarkCollectionModel>());
        private DataManager<BookmarkCollectionModel> _Data;

        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.B, KeyboardModifiers.Control);
                yield return new InputTrigger("bookmark", "bm");
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.CanBookmark(out var bookmark)) {
                if (context.HasPanel(out var active)) {
                    var value = active?.ID?.Value;
                    var model = await Data.Load(cancellationToken);
                    var provider = bookmark.GetType().AssemblyQualifiedName;
                    var existing = model
                        .Items
                        .Where(item => item.Provider == provider)
                        .Select(item => new KeyValuePair<string, string>(item.Key, item.Value))
                        .ToList();
                    var newItem = await bookmark.MakeFrom(value, existing, cancellationToken);
                    model.Items = model.Items
                        .Append(new BookmarkModel {
                            Key = newItem.Key,
                            Value = newItem.Value,
                            Provider = provider
                        })
                        .DistinctBy(item => $"{item.Provider}>{item.Value}")
                        .ToArray();
                    return true;
                }
            }
            return false;
        }
    }
}
