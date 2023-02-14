using System;

namespace Domore.Logs {
    internal delegate void LogTypeSeverityChangedEvent(object sender, LogTypeSeverityChangedEventArgs e);

    internal class LogTypeSeverityChangedEventArgs : EventArgs {
        public string TypeName { get; }
        public LogSeverity? Severity { get; }

        public LogTypeSeverityChangedEventArgs(LogSeverity? severity, string typeName = null) {
            TypeName = typeName;
            Severity = severity;
        }

    }
}
