using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Brows.IPC.MessageSubscriptions;

internal sealed class ProxySubscription : CommanderMessageSubscription {
    internal IAsyncEnumerable<CommanderMessage> Agent { get; }

    internal ProxySubscription(IAsyncEnumerable<CommanderMessage> agent) {
        Agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }

    public sealed override async
    IAsyncEnumerable<CommanderMessage> Messages([EnumeratorCancellation] CancellationToken token) {
        await foreach (var item in Agent.WithCancellation(token)) {
            yield return item;
        }
    }
}
