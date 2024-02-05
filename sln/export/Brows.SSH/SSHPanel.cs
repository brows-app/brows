using Brows.SSH;
using System;

namespace Brows {
    public static class SSHPanel {
        public static bool HasSSHClient(this IPanel panel, out SSHClientBase sshClient) {
            if (null == panel) throw new ArgumentNullException(nameof(panel));
            if (true == panel.HasProvider(out SSHProvider provider)) {
                sshClient = provider.ClientReady();
                return sshClient != null;
            }
            sshClient = null;
            return false;
        }
    }
}
