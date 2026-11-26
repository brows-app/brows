using Brows.Composition;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Reads the result of a completed update handoff.
/// </summary>
public interface IAppUpdateOutcome : IExport {
    /// <summary>
    /// Returns and removes the last unread outcome, or returns null when none is available.
    /// Does not contact the network.
    /// </summary>
    Task<AppUpdateOutcome> Get(CancellationToken token);
}
