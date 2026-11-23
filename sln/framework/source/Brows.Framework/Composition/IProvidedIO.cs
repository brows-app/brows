using Brows.Entries;
using System.Collections.Generic;

namespace Brows.Composition;

/// <summary>
/// Supplies sets of files or streams for a transfer operation.
/// </summary>
public interface IProvidedIO {
    /// <summary>
    /// Gets the sets of files or streams available for transfer.
    /// </summary>
    IEnumerable<IEntryStreamSet> StreamSets { get; }
}
