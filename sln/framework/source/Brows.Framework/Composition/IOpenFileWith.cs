using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Opens a file with a chosen application.
/// </summary>
public interface IOpenFileWith : IExport {
    /// <summary>
    /// Opens a file with the chosen application.
    /// </summary>
    /// <param name="file">
    /// The file path to open.
    /// </param>
    /// <param name="with">
    /// The application used to open the file.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the request was handled; otherwise, false.
    /// </returns>
    Task<bool> Work(string file, string with, CancellationToken token);
}
