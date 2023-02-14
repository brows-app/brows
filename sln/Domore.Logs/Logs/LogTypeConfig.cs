using System;

namespace Domore.Logs {
    internal class LogTypeConfig {
        public event EventHandler SeverityChanged;

        public string Format { get; set; }

        public string Name { get; }

        public LogTypeConfig(string name = null) {
            Name = name;
        }

        public LogSeverity? Severity {
            get => _Severity;
            set {
                if (_Severity != value) {
                    _Severity = value;
                    SeverityChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        private LogSeverity? _Severity;
    }
}
