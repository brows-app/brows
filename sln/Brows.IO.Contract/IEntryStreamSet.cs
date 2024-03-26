using System.Collections.Generic;
using System.Threading;

namespace Brows {
    public interface IEntryStreamSet {
        IEntryStreamReady StreamSourceReady();
        IEnumerable<string> FileSource();
        IAsyncEnumerable<IEntryStreamSource> StreamSource(CancellationToken token);
    }
}
