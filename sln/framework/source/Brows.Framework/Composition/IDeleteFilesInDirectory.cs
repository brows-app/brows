using Brows.Operations;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Deletes file-system paths within a directory.
/// </summary>
public interface IDeleteFilesInDirectory : IExport {
    /// <summary>
    /// Deletes the supplied files or directories.
    /// </summary>
    /// <param name="files">
    /// The paths to delete.
    /// </param>
    /// <param name="directory">
    /// The directory containing the items.
    /// </param>
    /// <param name="options">
    /// The deletion settings.
    /// </param>
    /// <param name="progress">
    /// Receives progress updates for the operation.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the operation succeeded; otherwise, false.
    /// </returns>
    Task<bool> Work(IEnumerable<string> files,
                    string directory,
                    IDeleteFilesInDirectoryOptions options,
                    IOperationProgress progress,
                    CancellationToken token);
}
