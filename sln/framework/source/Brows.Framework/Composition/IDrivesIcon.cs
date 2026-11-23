using System;
using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Retrieves an icon for a collection of drives.
/// </summary>
public interface IDrivesIcon : IExport {
    /// <summary>
    /// Retrieves an icon for a collection of drives.
    /// </summary>
    /// <param name="drives">
    /// The collection to represent.
    /// </param>
    /// <param name="set">
    /// Receives the platform-specific icon.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if an icon was retrieved; otherwise, false.
    /// </returns>
    Task<bool> Work(IDrives drives, Action<object> set, CancellationToken token);
}
