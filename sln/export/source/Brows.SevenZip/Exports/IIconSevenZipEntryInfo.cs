using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports;

/// <summary>
/// Provides an icon for an item stored in a 7-Zip archive.
/// </summary>
public interface IIconSevenZipEntryInfo : IExport {
    /// <summary>
    /// Gets the icon for the specified archive item.
    /// </summary>
    /// <param name="sevenZipEntryInfo">
    /// The file or directory whose icon is requested.
    /// </param>
    /// <param name="token">
    /// A token that requests cancellation.
    /// </param>
    /// <returns>
    /// A task containing the icon object, or <see langword="null"/> when no icon is available.
    /// </returns>
    Task<object> Icon(SevenZipEntryInfo sevenZipEntryInfo, CancellationToken token);
}
