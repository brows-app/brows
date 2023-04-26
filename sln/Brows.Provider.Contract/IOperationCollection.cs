using System.Collections.Generic;

namespace Brows {
    public interface IOperationCollection : IEnumerable<IOperation> {
        int Count { get; }
        void Add(IOperation item);
        bool Remove(IOperation item);
        void Clear();
    }
}
