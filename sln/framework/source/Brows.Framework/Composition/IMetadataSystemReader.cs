using Brows.Operations;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition;

public interface IMetadataSystemReader : IExport {
    Task<bool> Work(ICollection<IMetadataDefinition> definitions, string file, IOperationProgress progress, CancellationToken token);
}
