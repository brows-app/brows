using Brows.Operations;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Reads property values from a file.
/// </summary>
public interface IMetadataFileReader : IExport {
    /// <summary>
    /// Reads file property values for the supplied definitions.
    /// </summary>
    /// <param name="file">
    /// The file to inspect.
    /// </param>
    /// <param name="values">
    /// The definitions to read, paired with the values that will be filled in.
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
    Task<bool> Work(string file,
                    IDictionary<IMetadataDefinition, IMetadataValue> values,
                    IOperationProgress progress,
                    CancellationToken token);
}
