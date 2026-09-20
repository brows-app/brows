using Brows.Entries;
using System.Collections.Generic;

namespace Brows.Composition;

public sealed class ProvidedIO : IProvidedIO {
    public IEnumerable<IEntryStreamSet> StreamSets { get; set; }
}
