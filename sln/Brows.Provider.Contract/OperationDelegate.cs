using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public delegate Task OperationDelegate(IOperationProgress progress, CancellationToken token);
}
