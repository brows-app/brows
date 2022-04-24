using System;

namespace Brows.Logger.Services {
    public abstract class LogServiceBase : ILogService {
        public event EventHandler SeverityChanged;

        public LogSeverity DesiredSeverity {
            get => _DesiredSeverity;
            set {
                if (_DesiredSeverity != value) {
                    _DesiredSeverity = value;
                    SeverityChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        private LogSeverity _DesiredSeverity;

        public LogSeverity ActualSeverity {
            get => _ActualSeverity ?? (_ActualSeverity = DesiredSeverity).Value;
            protected set {
                if (_ActualSeverity != value) {
                    _ActualSeverity = value;
                    SeverityChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        private LogSeverity? _ActualSeverity;

        public abstract void Log(string s);
    }
}
