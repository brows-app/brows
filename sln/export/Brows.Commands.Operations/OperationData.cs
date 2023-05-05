using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    internal sealed class OperationData : CommandContextData<IOperation> {
        protected sealed override CommandContextData<IOperation> Create(int index, IList<IOperation> list) {
            return new OperationData(Command, index, Collection);
        }

        protected sealed override void Removed(IOperation item) {
            Collection.Remove(item);
        }

        protected sealed override void Cleared(IEnumerable<IOperation> items) {
        }

        public IOperationCollection Collection { get; }

        public OperationData(ICommand command, int index, IOperationCollection collection) : base(command, index, collection?.AsEnumerable()?.ToList()) {
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }
    }
}
