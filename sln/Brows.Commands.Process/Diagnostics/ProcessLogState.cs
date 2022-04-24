namespace Brows.Diagnostics {
    using ComponentModel;
    using Gui;

    internal class ProcessLogState : NotifyPropertyChanged {
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
