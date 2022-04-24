using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IPayloadTarget {
        Task Open(string id, CancellationToken cancellationToken);
        Task Refresh(CancellationToken cancellationToken);
    }
}
