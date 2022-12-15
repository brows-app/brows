using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IOperable {
        Task Operate(Func<IOperationProgress, CancellationToken, Task> task, CancellationToken? cancellationToken = null);
    }
}
