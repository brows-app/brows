using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Triggers;

    public interface ICommand {
        IEnumerable<InputTrigger> InputTriggers { get; }
        IEnumerable<KeyboardTrigger> KeyboardTriggers { get; }
        bool Arbitrary { get; }
        IAsyncEnumerable<ICommandSuggestion> SuggestAsync(ICommandContext context, CancellationToken cancellationToken);
        bool Workable(ICommandContext context);
        Task<bool> WorkAsync(ICommandContext context, CancellationToken cancellationToken);
    }
}
