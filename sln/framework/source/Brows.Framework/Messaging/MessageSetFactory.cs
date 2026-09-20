using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Messaging;

public sealed class MessageSetFactory {
    private readonly IReadOnlyList<IMessageFactory> Agent;

    public MessageSetFactory(IEnumerable<IMessageFactory> items) {
        ArgumentNullException.ThrowIfNull(items);
        Agent = items.Where(item => item != null).ToList().AsReadOnly();
    }

    public async Task<MessageSet> Create(object window, CancellationToken token) {
        var list = new List<IMessenger>();
        await Task
            .WhenAll(Agent
                .Select(item => item.Work(window, list, token))
                .Where(task => task != null));
        return new MessageSet(list);
    }
}
