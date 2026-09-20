using System.Threading;
using System.Threading.Tasks;

namespace Brows.Operations;

public delegate Task OperationDelegate(IOperationProgress progress, CancellationToken token);
