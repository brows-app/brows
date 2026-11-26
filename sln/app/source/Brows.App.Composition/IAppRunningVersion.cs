using Brows.Composition;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Provides the version of the running Brows process.
/// </summary>
public interface IAppRunningVersion : IExport {
    /// <summary>
    /// Returns the entry assembly's informational version without build metadata,
    /// or null when the attribute is missing. Performs no I/O.
    /// </summary>
    Task<string> Get(CancellationToken token);
}
