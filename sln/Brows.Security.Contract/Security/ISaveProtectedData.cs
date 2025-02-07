using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Security {
    public interface ISaveProtectedData : IExport {
        Task<bool> Work(IReadOnlyDictionary<string, byte[]> data, CancellationToken token);
    }
}
