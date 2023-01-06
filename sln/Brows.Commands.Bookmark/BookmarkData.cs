using System;

namespace Brows {
    using Config;
    using Threading.Tasks;

    internal class BookmarkData : CommandContextData, ICommandContextHint {
        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<BookmarkData>());
        private TaskHandler _TaskHandler;

        public override object Current => Collection;

        public BookmarkConfig Collection { get; }
        public ICommandContext Context { get; }

        public BookmarkData(ICommand command, ICommandContext context, BookmarkConfig collection) : base(command) {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }

        public override ICommandContextData Enter() {
            var item = Collection.CurrentItem;
            if (item != null) {
                TaskHandler.Begin(async cancellationToken => {
                    await Context.OpenOrAddPanel(item.Loc, cancellationToken);
                });
            }
            Flag = new CommandContextFlag { PersistInput = false };
            return this;
        }

        public override ICommandContextData Remove() {
            var item = Collection.CurrentItem;
            if (item != null) {
                TaskHandler.Begin(async cancellationToken => {
                    await Collection.Remove(item.Key, cancellationToken);
                });
            }
            return this;
        }

        public override ICommandContextData Clear() {
            TaskHandler.Begin(async cancellationToken => {
                await Collection.Clear(cancellationToken);
            });
            return this;
        }
    }
}
