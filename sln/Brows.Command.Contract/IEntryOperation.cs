using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IEntryOperation {
        string Name { get; }
        Func<IOperationProgress, CancellationToken, Task> Task { get; }
    }
}
