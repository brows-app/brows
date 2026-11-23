using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Retrieves an icon for a drive.
/// </summary>
public interface IDriveIcon : IExport {
    /// <summary>
    /// Retrieves an icon for a drive.
    /// </summary>
    /// <param name="info">
    /// The drive to represent.
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
    Task<bool> Work(DriveInfo info, Action<object> set, CancellationToken token);
}
