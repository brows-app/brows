using Brows.SSH;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    public static class SSHContext {
        public static bool HasSourceSSHFileInfo(this ICommandContext commandContext, out SSHFileInfo item, out IReadOnlyList<SSHFileInfo> list) {
            if (null == commandContext) throw new ArgumentNullException(nameof(commandContext));
            if (false == commandContext.HasSource<SSHEntry>(out var entry, out var entries)) {
                item = null;
                list = null;
                return false;
            }
            item = entry.Info;
            list = entries.Select(e => e.Info).ToList();
            return true;
        }
    }
}
