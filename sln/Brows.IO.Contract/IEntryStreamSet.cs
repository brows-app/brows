using System.Collections.Generic;

namespace Brows {
    public interface IEntryStreamSet {
        IEntryStreamReady StreamSourceReady();
        IEnumerable<IEntryStreamSource> StreamSource();
        IEnumerable<string> FileSource();
    }
}
