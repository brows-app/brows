using System;
using System.Security;

namespace Brows.SSH {
    internal sealed class SSHAuth : IDisposable {
        public SecureString Secret { get; set; }
        public string PublicKeyFile { get; set; }
        public string PrivateKeyFile { get; set; }

        void IDisposable.Dispose() {
            using (Secret) {
            }
        }
    }
}
