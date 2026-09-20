using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Commands;

internal static class CommandSuggestions {
    public static async IAsyncEnumerable<ICommandSuggestion> Empty() {
        await Task.CompletedTask;
        yield break;
    }
}
