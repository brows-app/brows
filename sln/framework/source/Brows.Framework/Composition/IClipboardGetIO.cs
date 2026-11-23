using Brows.Operations;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Reads files and stream sources from the clipboard.
/// </summary>
public interface IClipboardGetIO : IExport {
    /// <summary>
    /// Reads transfer sources and paste settings from the clipboard.
    /// </summary>
    /// <param name="collection">
    /// Receives the transfer sources.
    /// </param>
    /// <param name="data">
    /// Receives the paste settings.
    /// </param>
    /// <param name="progress">
    /// Receives progress updates for the operation.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the clipboard was read successfully; otherwise, false.
    /// </returns>
    Task<bool> Work(ICollection<IProvidedIO> collection, IClipboardGetIOData data, IOperationProgress progress, CancellationToken token);
}
