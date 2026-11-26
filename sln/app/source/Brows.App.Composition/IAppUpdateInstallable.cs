using Brows.Composition;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Identifies copies eligible for in-app installation.
/// </summary>
public interface IAppUpdateInstallable : IExport {
    /// <summary>
    /// Returns true if Brows runs from its per-user MSI directory with a parseable version
    /// and a supported process architecture. Performs no I/O.
    /// </summary>
    Task<bool> Get(CancellationToken token);
}
