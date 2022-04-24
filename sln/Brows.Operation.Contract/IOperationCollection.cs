using System.Collections.Generic;

namespace Brows {
    public interface IOperationCollection : IReadOnlyList<IOperation> {
        void Add(IOperation item);
        bool Remove(IOperation item);
        void Clear();
    }
}
