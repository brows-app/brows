using Brows.Detail;
using System;
using System.Collections.Generic;

namespace Brows.Commands;

internal sealed class TerminalClose : FileSystemCommand<TerminalCloseParameter> {
    protected sealed override IEnumerable<Type> Source =>
        null;

    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (context.HasPanel(out var active) == false) {
            return false;
        }
        if (active.HasProvider(out FileSystemProvider provider) == false) {
            return false;
        }
        if (provider.Detail.Active is not FileSystemTerminal terminal) {
            return true;
        }
        return context.Operate(async (progress, token) => {
            var closed = await provider.Detail.CloseAsync(terminal, token);
            if (closed) {
                return true;
            }
            return ReferenceEquals(provider.Detail.Active, terminal) == false;
        });
    }
}
