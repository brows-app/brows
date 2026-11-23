using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Opens a file with its associated application.
/// </summary>
public interface IOpenFile : IExport {
    /// <summary>
    /// Opens a file with its associated application.
    /// </summary>
    /// <param name="file">
    /// The file path to open.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the request was handled; otherwise, false.
    /// </returns>
    Task<bool> Work(string file, CancellationToken token);
}
