using Brows.Commands;
using System.Collections.Generic;

namespace Brows.SftpConnection.Commands;

internal sealed class Open : SftpConnectionCommand<Open.Parameter> {
    protected sealed override bool Work(Context context) {
        if (context is null || !context.HasSource(out SftpConnectionEntry _, out var entries)) {
            return false;
        }
        return context.Operate(async (progress, token) => {
            var worked = false;
            foreach (var entry in entries) {
                if (entry.Action is not null) {
                    worked |= await entry.Action(token);
                }
            }
            return worked;
        });
    }

    public sealed class Parameter {
    }
}
