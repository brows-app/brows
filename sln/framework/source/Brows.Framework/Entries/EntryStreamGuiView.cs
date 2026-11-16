using Domore.Notification;
using System.ComponentModel;

namespace Brows.Entries;

internal sealed class EntryStreamGuiView : Notifier, IEntryStreamGuiView {
    private static readonly PropertyChangedEventArgs ChangedEvent = new(nameof(Changed));
    private static readonly PropertyChangedEventArgs SuccessEvent = new(nameof(Success));
    private static readonly PropertyChangedEventArgs LoadingEvent = new(nameof(Loading));
    private static readonly PropertyChangedEventArgs[] ReadyEvent = [
        new PropertyChangedEventArgs(nameof(Ready))
    ];

    public bool Success {
        get;
        set {
            if (Change(ref field, value, SuccessEvent, ReadyEvent)) {
                Changed = true;
                State.Changed();
            }
        }
    }

    public bool Loading {
        get;
        set {
            if (Change(ref field, value, LoadingEvent, ReadyEvent)) {
                Changed = true;
                State.Changed();
            }
        }
    }

    public bool Ready => Loading == false && Success;

    public bool Changed {
        get;
        private set => Change(ref field, value, ChangedEvent);
    }

    public EntryStreamGuiState State { get; }

    public EntryStreamGuiView(EntryStreamGuiState state) {
        State = state;
    }

    void IEntryStreamGuiView.Change(bool? loading, bool? success) {
        if (success == true) {
            Success = success ?? Success;
            Loading = loading ?? Loading;
        }
        else {
            Loading = loading ?? Loading;
            Success = success ?? Success;
        }
    }
}
