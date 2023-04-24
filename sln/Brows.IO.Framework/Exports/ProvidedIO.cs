using System.Collections.Generic;

namespace Brows.Exports {
    public sealed class ProvidedIO : IProvidedIO {
        public IEnumerable<IEntryStreamSet> StreamSets { get; set; }
    }
}
