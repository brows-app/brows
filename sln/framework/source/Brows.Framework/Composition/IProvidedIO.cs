using Brows.Entries;
using System.Collections.Generic;

namespace Brows.Composition;

public interface IProvidedIO {
    IEnumerable<IEntryStreamSet> StreamSets { get; }
}
