using Brows.Operations;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition;

public interface IClipboardGetIO : IExport {
    Task<bool> Work(ICollection<IProvidedIO> collection, IClipboardGetIOData data, IOperationProgress progress, CancellationToken token);
}
