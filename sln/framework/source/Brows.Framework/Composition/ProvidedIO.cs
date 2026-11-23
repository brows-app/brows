using Brows.Entries;
using System.Collections.Generic;

namespace Brows.Composition;

/// <summary>
/// Supplies sets of files or streams for a transfer operation.
/// </summary>
public sealed class ProvidedIO : IProvidedIO {
    /// <summary>
    /// Gets or sets the sets of files or streams available for transfer.
    /// </summary>
    public IEnumerable<IEntryStreamSet> StreamSets { get; set; }
}
