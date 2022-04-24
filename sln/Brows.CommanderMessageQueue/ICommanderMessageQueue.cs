using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface ICommanderMessageQueue {
        Task Write(string s, CancellationToken cancellationToken);
        IAsyncEnumerable<string> Read(CancellationToken cancellationToken);
    }
}
