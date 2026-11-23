using Brows.Operations;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Places files or stream sources on the clipboard.
/// </summary>
public interface IClipboardSetIO : IExport {
    /// <summary>
    /// Places transfer sources on the clipboard with the supplied paste settings.
    /// </summary>
    /// <param name="collection">
    /// The transfer sources to place on the clipboard.
    /// </param>
    /// <param name="data">
    /// The paste settings to associate with the sources.
    /// </param>
    /// <param name="progress">
    /// Receives progress updates for the operation.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the clipboard was updated; otherwise, false.
    /// </returns>
    Task<bool> Work(IEnumerable<IProvidedIO> collection,
                    IClipboardSetIOData data,
                    IOperationProgress progress,
                    CancellationToken token);
}
