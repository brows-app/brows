using System;
using System.Linq;

namespace Brows {
    internal sealed class BookmarkData : CommandContextData, ICommandContextHint {
        public override object Current => Collection;

        public Bookmarks Collection { get; }
        public ICommandContext Context { get; }

        public BookmarkData(ICommand command, ICommandContext context, Bookmarks collection) : base(command) {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }

        public sealed override ICommandContextData Enter() {
            var item = Collection.CurrentItem();
            if (item != null) {
                var context = Context;
                if (context.HasCommander(out var commander)) {
                    context.Operate(async (progress, token) => {
                        return await item.Open(
                            commander,
                            context.HasPanel(out var active) ? active : null,
                            token);
                    });
                }
            }
            Flag = new CommandContextFlag { PersistInput = false };
            return this;
        }

        public sealed override ICommandContextData Remove() {
            var item = Collection.CurrentItem();
            if (item != null) {
                Collection.Bookmark = Collection.Bookmark
                    .Where(b => b.Key != item.Key)
                    .ToList();
            }
            return this;
        }

        public sealed override ICommandContextData Clear() {
            Collection.Bookmark = null;
            return this;
        }
    }
}
