using Brows.Operations;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Copies file-system paths into a directory.
/// </summary>
public interface ICopyFilesToDirectory : IExport {
    /// <summary>
    /// Copies files or directories into a destination directory.
    /// </summary>
    /// <param name="files">
    /// The source file or directory paths.
    /// </param>
    /// <param name="directory">
    /// The destination directory.
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
                    IOperationProgress progress,
                    CancellationToken token);
}
