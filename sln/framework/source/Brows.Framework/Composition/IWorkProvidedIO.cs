using Brows.Operations;
using Brows.Providers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition;

public interface IWorkProvidedIO {
    Task<bool> Work(IEnumerable<IProvidedIO> io, IProvider target, IOperationProgress progress, CancellationToken token);
}
