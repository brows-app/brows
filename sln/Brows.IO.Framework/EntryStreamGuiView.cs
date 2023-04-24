using Domore.Notification;
using System.ComponentModel;

namespace Brows {
    internal class EntryStreamGuiView : Notifier, IEntryStreamGuiView {
        private static readonly PropertyChangedEventArgs ChangedEvent = new(nameof(Changed));
        private static readonly PropertyChangedEventArgs SuccessEvent = new(nameof(Success));
        private static readonly PropertyChangedEventArgs LoadingEvent = new(nameof(Loading));
        private static readonly PropertyChangedEventArgs[] ReadyEvent = new[] {
            new PropertyChangedEventArgs(nameof(Ready))
        };

        public bool Success {
            get => _Success;
            set {
                if (Change(ref _Success, value, SuccessEvent, ReadyEvent)) {
                    Changed = true;
                }
            }
        }
        private bool _Success;

        public bool Loading {
            get => _Loading;
            set {
                if (Change(ref _Loading, value, LoadingEvent, ReadyEvent)) {
                    Changed = true;
                }
            }
        }
        private bool _Loading;

        public bool Ready =>
            Loading == false &&
            Success;

        public bool Changed {
            get => _Changed;
            private set => Change(ref _Changed, value, ChangedEvent);
        }
        private bool _Changed;

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
}
