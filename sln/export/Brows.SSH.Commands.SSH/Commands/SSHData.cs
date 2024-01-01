using Domore.Notification;
using System;
using System.Security;

namespace Brows.Commands {
    public sealed class SSHData : Notifier {
        public Uri URI { get; set; }
        public SecureString Secret { get; set; }
    }
}
