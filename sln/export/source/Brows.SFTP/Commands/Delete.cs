using System.Collections.Generic;
using System.Linq;

namespace Brows.Commands;

internal sealed class Delete : SftpCommand<Delete.Parameter> {
    protected sealed override IEnumerable<Type> Source { get; } = [typeof(SftpEntry)];

    protected sealed override bool Work(Context context) {
        if (context is null || !context.HasPanel(out var active) || !active.HasProvider(out SftpProvider provider)) {
            return false;
        }
        if (!context.HasSource(out SftpEntry _, out var entries)) {
            return false;
        }
        return context.Operate(async (_, token) => {
            foreach (var entry in entries.DistinctBy(entry => entry.ID)) {
                await provider.DeleteRecursive(entry, token);
            }
            await new ProviderPostponedRefresh(provider).Work(final: true, token);
            return true;
        });
    }

    public sealed class Parameter {
    }
}
