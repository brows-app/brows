using System;
using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Determines whether a directory distinguishes names by letter case.
/// </summary>
public interface ICaseSensitiveDirectory : IExport {
    /// <summary>
    /// Determines whether a directory uses case-sensitive names.
    /// </summary>
    /// <param name="directory">
    /// The directory to inspect.
    /// </param>
    /// <param name="set">
    /// Receives true for case-sensitive names, or false for case-insensitive names.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the directory setting was retrieved; otherwise, false.
    /// </returns>
    Task<bool> Work(string directory, Action<bool> set, CancellationToken token);
}
