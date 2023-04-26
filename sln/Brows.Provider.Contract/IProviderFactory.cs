using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IProviderFactory : IExport {
        Task<IProvider> CreateFor(string id, IPanel panel, CancellationToken cancellationToken);
    }
}
