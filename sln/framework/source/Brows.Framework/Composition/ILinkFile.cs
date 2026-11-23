using System;
using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Resolves the target of a link file.
/// </summary>
public interface ILinkFile : IExport {
    /// <summary>
    /// Resolves the target path of a link file.
    /// </summary>
    /// <param name="file">
    /// The link file path.
    /// </param>
    /// <param name="set">
    /// Receives the target path.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the target was resolved; otherwise, false.
    /// </returns>
    Task<bool> Work(string file, Action<string> set, CancellationToken cancellationToken);
}
