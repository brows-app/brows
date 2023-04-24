using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Export {
    public interface IBookmark : IEntryProviderExport {
        Task<bool> Work(
            IReadOnlyList<KeyValuePair<string, string>> existing,
            IList<KeyValuePair<string, string>> added,
            IEntryProvider target,
            IOperationProgress progress,
            CancellationToken token);
    }
}
