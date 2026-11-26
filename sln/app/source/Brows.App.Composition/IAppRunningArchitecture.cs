using Brows.Composition;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Provides the installer architecture of the running process.
/// </summary>
public interface IAppRunningArchitecture : IExport {
    /// <summary>
    /// Returns x64, arm64, or null for an unsupported process architecture. Performs no I/O.
    /// </summary>
    Task<string> Get(CancellationToken token);
}
