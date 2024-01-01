using System;
using System.Collections.Generic;

namespace Brows.SSHConnection.Commands {
    internal sealed class Open : SSHConnectionCommand<Open.Parameter> {
        protected sealed override IEnumerable<Type> Source { get; } = new[] {
            typeof(SSHConnectionEntry)
        };

        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == context.GetParameter(out var parameter)) return false;
            if (false == context.HasSource(out SSHConnectionEntry _, out var entries)) return false;
            return context.Operate(async (progress, token) => {
                var worked = false;
                foreach (var entry in entries) {
                    var task = entry.Task;
                    worked |= (task != null && await task(token));
                }
                return worked;
            });
        }

        public sealed class Parameter {
        }
    }
}
