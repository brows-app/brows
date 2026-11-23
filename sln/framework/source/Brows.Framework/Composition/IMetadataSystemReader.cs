using Brows.Operations;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Retrieves the available file property definitions.
/// </summary>
public interface IMetadataSystemReader : IExport {
    /// <summary>
    /// Collects the available property definitions for a file or the system.
    /// </summary>
    /// <param name="definitions">
    /// Receives the property definitions.
    /// </param>
    /// <param name="file">
    /// The file to inspect, or null to request system property definitions.
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
    Task<bool> Work(ICollection<IMetadataDefinition> definitions,
                    string file,
                    IOperationProgress progress,
                    CancellationToken token);
}
