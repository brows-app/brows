using Brows.Operations;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition;

public interface IClipboardSetIO : IExport {
    Task<bool> Work(IEnumerable<IProvidedIO> collection, IClipboardSetIOData data, IOperationProgress progress, CancellationToken token);
}
