using Brows.Composition;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Looks up a release for an update request.
/// </summary>
public interface IAppUpdateAvailable : IExport {
    /// <summary>
    /// Queries GitHub without using a cache. Returns null when no matching newer release exists.
    /// Throws for a null request, an invalid version or architecture, an unparseable running version,
    /// or a network, HTTP, or JSON failure. Works from any install location.
    /// </summary>
    Task<AppUpdate> Get(AppRequest request, CancellationToken token);
}
