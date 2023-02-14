using System;

namespace Domore.Logs {
    internal sealed class LogServiceProxy {
        private readonly object Locker = new object();

        public LogServiceConfig Config {
            get {
                if (_Config == null) {
                    lock (Locker) {
                        if (_Config == null) {
                            _Config = new LogServiceConfig();
                        }
                    }
                }
                return _Config;
            }
        }
        private LogServiceConfig _Config;

        public ILogService Agent { get; }

        public LogServiceProxy(ILogService agent) {
            Agent = agent ?? throw new ArgumentNullException(nameof(agent));
        }

        public void Log(LogEntry entry) {
            if (null == entry) throw new ArgumentNullException(nameof(entry));
            var name = entry.LogName;
            var limit = Config[name].Severity ?? Config.Default.Severity;
            if (limit.HasValue && limit.Value <= entry.LogSeverity) {
                var frmt = Config[name].Format ?? Config.Default.Format;
                var data = entry.LogData(frmt);
                Agent.Log(name, data);
            }
        }
    }
}
