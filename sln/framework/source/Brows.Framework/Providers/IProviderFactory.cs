using Brows.Composition;
using Brows.Panels;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Providers;

public interface IProviderFactory : IExport {
    Task<IProvider> CreateFor(string id, IPanel panel, CancellationToken cancellationToken);
}
