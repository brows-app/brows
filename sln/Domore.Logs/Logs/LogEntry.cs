using System;
using System.Collections.Generic;
using System.Linq;

namespace Domore.Logs {
    internal sealed class LogEntry {
        private static readonly IReadOnlyDictionary<LogSeverity, string> Sev = new Dictionary<LogSeverity, string> {
            { LogSeverity.Critical, "crt" },
            { LogSeverity.Debug, "dbg" },
            { LogSeverity.Error, "err" },
            { LogSeverity.Info, "inf" },
            { LogSeverity.Warn, "wrn" }
        };

        private readonly Dictionary<string, string> Format = new Dictionary<string, string>();

        private string GetFormat(string format) {
            var s = format.Replace("{log}", LogName).Replace("{sev}", Sev[LogSeverity]);
            var logList = LogList;
            if (logList.Count == 1) {
                return s == ""
                    ? logList[0]
                    : s + " " + logList[0];
            }
            return s == ""
                ? string.Join(Environment.NewLine, logList)
                : s + Environment.NewLine + string.Join(Environment.NewLine, logList
                    .Select(d => string.Join(Environment.NewLine, d
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => $"    {line}"))));
        }

        public Type LogType { get; }
        public LogSeverity LogSeverity { get; }
        public IReadOnlyList<string> LogList { get; }

        public string LogName =>
            _LogName ?? (
            _LogName = LogType.Name);
        private string _LogName;

        public LogEntry(Type logType, LogSeverity logSeverity, IReadOnlyList<string> logList) {
            if (null == logType) throw new ArgumentNullException(nameof(logType));
            if (null == logList) throw new ArgumentNullException(nameof(logList));
            LogType = logType;
            LogList = logList;
            LogSeverity = logSeverity;
        }

        public string LogData(string format) {
            var key = format ?? "";
            if (Format.TryGetValue(key, out var value) == false) {
                Format[key] = value = GetFormat(key);
            }
            return value;
        }
    }
}
