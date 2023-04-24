using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IProvideIO : IEntryProviderExport {
        Task<bool> Work(ICollection<IProvidedIO> io, IEntryProvider target, IOperationProgress progress, CancellationToken token);
    }
}
