using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IClipboardSetIO : IExport {
        Task<bool> Work(IEnumerable<IProvidedIO> collection, IClipboardSetIOData data, IOperationProgress progress, CancellationToken token);
    }
}
