using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Logger {
    internal sealed class LogServiceCollection {
        private static readonly Dictionary<LogSeverity, string> Sev = new Dictionary<LogSeverity, string> {
            { LogSeverity.Critical, "crt" },
            { LogSeverity.Debug, "dbg" },
            { LogSeverity.Error, "err" },
            { LogSeverity.Info, "inf" },
            { LogSeverity.Warn, "wrn" }
        };

        private readonly ConcurrentBag<ILogService> List = new ConcurrentBag<ILogService>();
        private LogSeverity Severity;

        private void Item_SeverityChanged(object sender, EventArgs e) {
            Severity = List.Count == 0
                ? LogSeverity.None
                : List
                    .Where(item => item.ActualSeverity != LogSeverity.None)
                    .OrderBy(item => item.ActualSeverity)
                    .Select(item => item.ActualSeverity)
                    .FirstOrDefault();
        }

        public void Add(ILogService item) {
            if (null == item) throw new ArgumentNullException(nameof(item));
            item.SeverityChanged += Item_SeverityChanged;
            List.Add(item);
            Severity = Severity < item.ActualSeverity
                ? Severity == LogSeverity.None
                    ? item.ActualSeverity
                    : Severity
                : item.ActualSeverity;
        }

        public bool Log(LogSeverity severity, Type type) {
            return
                severity != LogSeverity.None &&
                severity >= Severity;
        }

        public void Log(LogSeverity severity, Type type, object[] data) {
            var sev = Sev[severity];
            var typ = type?.Name;
            var msg = string.Join(Environment.NewLine, data.Select(d => $"{sev} {typ} {d}"));
            foreach (var item in List) {
                if (item.ActualSeverity != LogSeverity.None && item.ActualSeverity <= severity) {
                    item.Log(msg);
                }
            }
        }
    }
}
