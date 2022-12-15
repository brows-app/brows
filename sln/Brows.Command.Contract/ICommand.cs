using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Triggers;

    public interface ICommand {
        IEnumerable<InputTrigger> InputTriggers { get; }
        IEnumerable<KeyboardTrigger> KeyboardTriggers { get; }
        bool Arbitrary { get; }
        IAsyncEnumerable<ICommandSuggestion> Suggest(ICommandContext context, CancellationToken cancellationToken);
        bool Workable(ICommandContext context);
        Task<bool> Work(ICommandContext context, CancellationToken cancellationToken);
    }
}
