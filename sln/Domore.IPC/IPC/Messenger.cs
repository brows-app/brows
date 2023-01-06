using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.IPC {
    public abstract class Messenger {
        public abstract Task Send(string message, CancellationToken cancellationToken);
        public abstract IAsyncEnumerable<string> Receive(CancellationToken cancellationToken);
    }
}
