using System;
using System.Collections.Generic;

namespace Domore.Logs {
    internal class LogServiceConfig {
        private readonly object Locker = new object();
        private readonly Dictionary<string, LogTypeConfig> Type = new Dictionary<string, LogTypeConfig>();

        private void Type_SeverityChanged(object sender, EventArgs e) {
            var config = (LogTypeConfig)sender;
            var args = new LogTypeSeverityChangedEventArgs(config.Severity, config.Name);
            TypeSeverityChanged?.Invoke(this, args);
        }

        private void Default_SeverityChanged(object sender, EventArgs e) {
            var args = new LogTypeSeverityChangedEventArgs(Default.Severity);
            DefaultSeverityChanged?.Invoke(this, args);
        }

        public event LogTypeSeverityChangedEvent TypeSeverityChanged;
        public event LogTypeSeverityChangedEvent DefaultSeverityChanged;

        public LogTypeConfig this[string name] {
            get {
                lock (Type) {
                    if (Type.TryGetValue(name, out var value) == false) {
                        Type[name] = value = new LogTypeConfig(name);
                        Type[name].SeverityChanged += Type_SeverityChanged;
                    }
                    return value;
                }
            }
        }

        public LogTypeConfig Default {
            get {
                if (_Default == null) {
                    lock (Locker) {
                        if (_Default == null) {
                            _Default = new LogTypeConfig();
                            _Default.SeverityChanged += Default_SeverityChanged;
                        }
                    }
                }
                return _Default;
            }
        }
        private LogTypeConfig _Default;

        public IEnumerable<string> Names {
            get {
                lock (Type) {
                    return new List<string>(Type.Keys);
                }
            }
        }
    }
}
