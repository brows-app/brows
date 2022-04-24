using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Brows.Logger {
    internal class Logger : ILog {
        public Type Type { get; }
        public Logging Logging { get; }

        public Logger(Type type, Logging logging) {
            Type = type;
            Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        public bool Enabled(LogSeverity severity) {
            return Logging.Log(this, severity);
        }

        public void Data(LogSeverity severity, params object[] data) {
            Logging.Log(this, severity, data);
        }

        public bool Debug() {
            return Enabled(LogSeverity.Debug);
        }

        public void Debug(params object[] data) {
            Data(LogSeverity.Debug, data);
        }

        public bool Info() {
            return Enabled(LogSeverity.Info);
        }

        public void Info(params object[] data) {
            Data(LogSeverity.Info, data);
        }

        public bool Warn() {
            return Enabled(LogSeverity.Warn);
        }

        public void Warn(params object[] data) {
            Data(LogSeverity.Warn, data);
        }

        public bool Error() {
            return Enabled(LogSeverity.Error);
        }

        public void Error(params object[] data) {
            Data(LogSeverity.Error, data);
        }

        public bool Critical() {
            return Enabled(LogSeverity.Critical);
        }

        public void Critical(params object[] data) {
            Data(LogSeverity.Critical, data);
        }

        public async Task Performance(string data, Func<Task> task) {
            if (null == task) throw new ArgumentNullException(nameof(task));
            var s = Info() ? Stopwatch.StartNew() : null;
            try {
                await task();
            }
            finally {
                if (s != null) {
                    s.Stop();
                    if (Info()) {
                        Info(
                            nameof(Performance),
                            $"{nameof(data)} > {data}",
                            $"{nameof(s.Elapsed)} > {s.Elapsed}");
                    }
                }
            }
        }
    }
}
