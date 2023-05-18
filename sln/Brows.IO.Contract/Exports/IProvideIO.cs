using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IProvideIO : IProviderExport {
        Task<bool> Work(ICollection<IProvidedIO> io, ICommandSource source, IProvider target, IOperationProgress progress, CancellationToken token);
    }
}
