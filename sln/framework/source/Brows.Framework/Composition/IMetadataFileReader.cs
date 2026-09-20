using Brows.Operations;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition;

public interface IMetadataFileReader : IExport {
    Task<bool> Work(string file, IDictionary<IMetadataDefinition, IMetadataValue> values, IOperationProgress progress, CancellationToken token);
}
