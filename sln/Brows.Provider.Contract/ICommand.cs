using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface ICommand : IExport {
        bool Enabled { get; }
        object Config { get; }
        ICommandTrigger Trigger { get; }
        IAsyncEnumerable<ICommandSuggestion> Suggest(ICommandContext context, CancellationToken token);
        bool TriggeredWork(ICommandContext context);
        bool ArbitraryWork(ICommandContext context);
        Task Init(CancellationToken token);
    }
}
