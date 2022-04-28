using System;

namespace Brows {
    using Data;
    using Threading.Tasks;

    internal class BookmarkData : CommandContextData, ICommandContextHint {
        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<BookmarkData>());
        private TaskHandler _TaskHandler;

        public override object Current => Collection;

        public BookmarkCollection Collection { get; }
        public ICommandContext Context { get; }

        public BookmarkData(ICommand command, ICommandContext context, BookmarkCollection collection) : base(command) {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }

        public override ICommandContextData Enter() {
            var item = Collection.Controller?.CurrentItem;
            if (item != null) {
                TaskHandler.Begin(async token => {
                    await Context.OpenOrAddPanel(item.Value, token);
                });
            }
            Flag = new CommandContextFlag { PersistInput = false };
            return this;
        }

        public override ICommandContextData Remove() {
            var item = Collection.Controller?.CurrentItem;
            if (item != null) {
                Collection.Remove(new[] { item.Key });
            }
            return this;
        }

        public override ICommandContextData Clear() {
            Collection.Clear();
            return this;
        }
    }
}
