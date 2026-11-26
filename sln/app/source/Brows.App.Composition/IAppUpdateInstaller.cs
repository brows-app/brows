using Brows.Composition;
using Brows.Operations;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Verifies an MSI and starts the update handoff.
/// </summary>
public interface IAppUpdateInstaller : IExport {
    /// <summary>
    /// Validates the release and downloads or reuses an MSI verified against GitHub's published digest.
    /// The per-user cache is trusted to the same extent as the running process. Reports byte progress.
    /// Returns true when the helper has started and the caller must promptly shut down Brows.
    /// Returns false if another update is in progress. Throws for a non-installable copy, an invalid update,
    /// or a failure to download, verify, or start the helper.
    /// </summary>
    Task<bool> Work(AppUpdate update, IOperationProgress progress, CancellationToken token);
}
