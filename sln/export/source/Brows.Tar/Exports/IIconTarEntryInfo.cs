using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports;

/// <summary>
/// Provides an icon for an item stored in a tar archive.
/// </summary>
public interface IIconTarEntryInfo : IExport {
    /// <summary>
    /// Gets the icon for the specified archive item.
    /// </summary>
    /// <param name="tarEntryInfo">
    /// The file or directory whose icon is requested.
    /// </param>
    /// <param name="token">
    /// A token that requests cancellation.
    /// </param>
    /// <returns>
    /// A task containing the icon object, or <see langword="null"/> when no icon is available.
    /// </returns>
    Task<object> Icon(TarEntryInfo tarEntryInfo, CancellationToken token);
}
