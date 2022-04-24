using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IEntryBrowser {
        Task<bool> Browse(string id, CancellationToken cancellationToken);
    }
}
