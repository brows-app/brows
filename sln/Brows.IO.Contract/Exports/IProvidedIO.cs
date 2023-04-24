using System.Collections.Generic;

namespace Brows.Exports {
    public interface IProvidedIO {
        IEnumerable<IEntryStreamSet> StreamSets { get; }
    }
}
