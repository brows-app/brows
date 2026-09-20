using System.Collections.Generic;

namespace Brows.Operations;

public interface IOperationCollection {
    int Count { get; }
    bool Remove(IOperation item);
    IEnumerator<IOperation> GetEnumerator();
    IEnumerable<IOperation> AsEnumerable();
}
