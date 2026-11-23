using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Commands;

/// <summary>
/// Creates additional commander windows.
/// </summary>
public interface ICommanderDomain {
    /// <summary>
    /// Opens another commander with the requested panel locations.
    /// </summary>
    /// <param name="panels">
    /// The provider identifiers to open in the new commander.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the commander was opened; otherwise, false.
    /// </returns>
    Task<bool> AddCommander(IReadOnlyList<string> panels, CancellationToken token);
}
