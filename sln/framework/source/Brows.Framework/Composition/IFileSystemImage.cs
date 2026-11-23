using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Retrieves an image for a file-system item at a requested size.
/// </summary>
public interface IFileSystemImage : IExport {
    /// <summary>
    /// Retrieves an image for a file-system item at the requested size.
    /// </summary>
    /// <param name="info">
    /// The item to represent.
    /// </param>
    /// <param name="width">
    /// The requested image width.
    /// </param>
    /// <param name="height">
    /// The requested image height.
    /// </param>
    /// <param name="set">
    /// Receives the platform-specific image.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if an image was retrieved; otherwise, false.
    /// </returns>
    Task<bool> Work(FileSystemInfo info, int width, int height, Action<object> set, CancellationToken token);
}
