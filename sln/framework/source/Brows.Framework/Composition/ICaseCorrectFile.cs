using System;
using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Retrieves a path with the letter case stored by the file system.
/// </summary>
public interface ICaseCorrectFile : IExport {
    /// <summary>
    /// Retrieves the file-system spelling of a path.
    /// </summary>
    /// <param name="file">
    /// The path to look up.
    /// </param>
    /// <param name="set">
    /// Receives the path with the letter case stored by the file system.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the path was retrieved; otherwise, false.
    /// </returns>
    Task<bool> Work(string file, Action<string> set, CancellationToken token);
}
