using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface ICommand {
        ICommandTrigger Trigger { get; }
        bool Arbitrary { get; }
        IAsyncEnumerable<ICommandSuggestion> Suggest(ICommandContext context, CancellationToken cancellationToken);
        bool Workable(ICommandContext context);
        Task<bool> Work(ICommandContext context, CancellationToken cancellationToken);
        Task Init(CancellationToken cancellationToken);
    }
}
