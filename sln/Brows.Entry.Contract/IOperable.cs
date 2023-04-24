using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IOperable {
        void Operate(Func<IOperationProgress, CancellationToken, Task> task);
    }
}
