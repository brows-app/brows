using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports;

/// <summary>
/// Provides an icon for a tar archive file.
/// </summary>
public interface IIconTarFileInfo : IExport {
    /// <summary>
    /// Gets the icon for the specified archive file.
    /// </summary>
    /// <param name="tarFileInfo">
    /// The archive file whose icon is requested.
    /// </param>
    /// <param name="token">
    /// A token that requests cancellation.
    /// </param>
    /// <returns>
    /// A task containing the icon object, or <see langword="null"/> when no icon is available.
    /// </returns>
    Task<object> Icon(FileInfo tarFileInfo, CancellationToken token);
}
