using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IClipboardGetIO : IExport {
        Task<bool> Work(ICollection<IProvidedIO> collection, IOperationProgress progress, CancellationToken token);
    }
}
