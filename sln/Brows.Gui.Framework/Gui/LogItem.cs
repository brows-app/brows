using Domore.Logs;
using Domore.Notification;

namespace Brows.Gui {
    public class LogItem : Notifier {
        public string Message {
            get => _Message;
            set => Change(ref _Message, value, nameof(Message));
        }
        private string _Message;

        public LogSeverity Severity {
            get => _Severity;
            set => Change(ref _Severity, value, nameof(Severity));
        }
        private LogSeverity _Severity;
    }
}
