using System;

namespace Brows.Logger {
    public interface ILogService {
        event EventHandler SeverityChanged;
        LogSeverity DesiredSeverity { get; }
        LogSeverity ActualSeverity { get; }
        void Log(string s);
    }
}
