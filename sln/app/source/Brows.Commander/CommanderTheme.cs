using Domore.Notification;

namespace Brows;

public sealed class CommanderTheme : Notifier {
    public string Base {
        get;
        set => Change(ref field, value);
    }

    public string Background {
        get;
        set => Change(ref field, value);
    }

    public string Foreground {
        get;
        set => Change(ref field, value);
    }
}
