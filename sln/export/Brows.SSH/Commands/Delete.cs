using System;
using System.Collections.Generic;

namespace Brows.Commands {
    internal sealed class Delete : SSHCommand<Delete.Parameter> {
        protected sealed override IEnumerable<Type> Source { get; } = new[] {
            typeof(SSHEntry)
        };

        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasSourceSSHFileInfo(out _, out var list)) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == active.HasSSHClient(out var client)) return false;
            if (false == active.HasProvider(out SSHProvider provider)) return false;
            return context.Operate(async (progress, token) => {
                var refresh = new ProviderPostponedRefresh(provider);
                foreach (var item in list) {
                    await client.Delete(item, token);
                    await refresh.Work(token);
                }
                await refresh.Work(final: true, token);
                return true;
            });
        }

        public sealed class Parameter {
        }
    }
}
