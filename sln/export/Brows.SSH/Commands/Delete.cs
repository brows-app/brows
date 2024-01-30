using System;
using System.Collections.Generic;

namespace Brows.Commands {
    internal sealed class Delete : SSHCommand<Delete.Parameter> {
        protected sealed override IEnumerable<Type> Source { get; } = new[] {
            typeof(SSHEntry)
        };

        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasSSHClient(out var client)) return false;
            if (false == context.HasSourceSSHFileInfo(out _, out var list)) {
                return false;
            }
            return context.Operate(async (progress, token) => {
                foreach (var item in list) {
                    await client.Delete(item, token);
                }
                return true;
            });
        }

        public sealed class Parameter {
        }
    }
}
