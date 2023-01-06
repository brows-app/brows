using Domore.Notification;

namespace Brows.Diagnostics {
    using Gui;

    internal class ProcessLogState : Notifier {
        public LogItem LogItem {
            get => _LogItem;
            set => Change(ref _LogItem, value, nameof(LogItem));
        }
        private LogItem _LogItem;

        public Logbook Logbook { get; }

        public ProcessLogState(Logbook logbook) {
            Logbook = logbook;
        }
    }
}
