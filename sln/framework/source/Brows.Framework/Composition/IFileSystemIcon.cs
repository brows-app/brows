using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Retrieves an icon for a file or directory.
/// </summary>
public interface IFileSystemIcon : IExport {
    /// <summary>
    /// Retrieves an icon for a file or directory.
    /// </summary>
    /// <param name="info">
    /// The item to represent.
    /// </param>
    /// <param name="hint">
    /// Optional choices for the icon appearance.
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
    Task<bool> Work(FileSystemInfo info, IFileSystemIconHint hint, Action<object> set, CancellationToken token);
}
