using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IMetadataFileReader : IExport {
        Task<bool> Work(string file, IDictionary<IMetadataDefinition, IMetadataValue> values, IOperationProgress progress, CancellationToken token);
    }
}
