using System;
using System.Collections.Generic;

namespace Brows {
    internal class OperationData : CommandContextData<IOperation> {
        protected override CommandContextData<IOperation> Create(int index, IList<IOperation> list) {
            return new OperationData(Command, index, Collection);
        }

        protected override void Removed(IOperation item) {
            Collection.Remove(item);
        }

        protected override void Cleared(IEnumerable<IOperation> items) {
            Collection.Clear();
        }

        public IOperationCollection Collection { get; }

        public OperationData(ICommand command, int index, IOperationCollection collection) : base(command, index, new List<IOperation>(collection)) {
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }
    }
}
