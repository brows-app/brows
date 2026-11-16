using Domore.Notification;
using System.Collections.Generic;

namespace Brows.Panels;

internal sealed class SecretStore : Notifier {
    public Dictionary<string, string> Secret {
        get => field ??= [];
        set => Change(ref field, value ?? [], nameof(Secret));
    }
}
