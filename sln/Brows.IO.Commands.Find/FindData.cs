using System;
using System.Collections.Generic;
using System.IO;

namespace Brows {
    using Threading.Tasks;

    internal class FindData : CommandContextData<FindResult>, ICommandContextHint {
        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<FindData>());
        private TaskHandler _TaskHandler;

        private FindData(ICommand command, ICommandContext context, bool inputs, int index, IList<FindResult> list) : base(command, index, list) {
            Inputs = inputs;
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        protected override CommandContextData<FindResult> Create(int index, IList<FindResult> list) {
            return new FindData(Command, Context, true, index, list);
        }

        protected override void Cleared(IEnumerable<FindResult> items) {
            if (items != null) {
                foreach (var item in items) {
                    item.Die();
                }
            }
            base.Cleared(items);
        }

        protected override void Removed(FindResult item) {
            if (item != null) {
                item.Die();
            }
            base.Removed(item);
        }

        public override string Input => Inputs
            ? Item?.Input
            : null;

        public bool Inputs { get; }
        public ICommandContext Context { get; }

        public FindData(ICommand command, ICommandContext context, int index, IList<FindResult> list) : this(command, context, false, index, list) {
        }

        public override ICommandContextData Enter() {
            var findItem = Item?.CurrentItem;
            if (findItem != null) {
                var path = findItem.Info.FullName;
                var directory = Path.GetDirectoryName(path);
                TaskHandler.Begin(async token => {
                    await Context.OpenOrAddPanel(directory, token);
                });
            }
            Flag = new CommandContextFlag { PersistInput = false };
            return this;
        }
    }
}
