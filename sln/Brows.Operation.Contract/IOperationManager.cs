using System;
using System.Threading;

namespace Brows {
    public interface IOperationManager : IAsyncDisposable {
        IOperable Operable(string name);
        IOperationProgress Progress(CancellationToken cancellationToken, string name);
    }
}
