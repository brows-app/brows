using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Export {
    public interface IBookmark : IProviderExport {
        Task<bool> Work(
            IReadOnlyList<KeyValuePair<string, string>> existing,
            IList<KeyValuePair<string, string>> added,
            IProvider target,
            IOperationProgress progress,
            CancellationToken token);
    }
}
