using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IOperator : IAsyncDisposable {
        Task Deploy(CancellationToken cancellationToken);
    }
}
