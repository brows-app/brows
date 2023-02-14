using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Domore.Logs {
    internal sealed class LogServiceCollection {
        private readonly ConcurrentDictionary<string, LogServiceProxy> Set = new ConcurrentDictionary<string, LogServiceProxy>();
        private readonly ConcurrentDictionary<string, LogSeverity> TypeSeverity = new ConcurrentDictionary<string, LogSeverity>();
        private LogSeverity DefaultSeverity;

        private LogWork Work =>
            _Work ?? (
            _Work = new LogWork(nameof(LogServiceCollection), LogAction));
        private LogWork _Work;

        private LogServiceFactory Factory =>
            _Factory ?? (
            _Factory = new LogServiceFactory());
        private LogServiceFactory _Factory;

        private void SetSeverityChanged() {
            lock (Set) {
                var names = Set.SelectMany(item => item.Value.Config.Names).Distinct();
                foreach (var name in names) {
                    var severity = TypeSeverity[name] = Set
                        .Select(item => item.Value)
                        .Select(log => log.Config[name].Severity)
                        .Where(sev => sev.HasValue)
                        .Select(sev => sev.Value)
                        .Where(sev => sev != LogSeverity.None)
                        .OrderBy(sev => sev)
                        .FirstOrDefault();
                    if (severity == LogSeverity.None) {
                        TypeSeverity.Remove(name, out _);
                    }
                }
                DefaultSeverity = Set
                    .Select(item => item.Value)
                    .Select(log => log.Config.Default.Severity)
                    .Where(sev => sev.HasValue)
                    .Select(sev => sev.Value)
                    .Where(sev => sev != LogSeverity.None)
                    .OrderBy(sev => sev)
                    .FirstOrDefault();
            }
        }

        private void Config_DefaultSeverityChanged(object sender, LogTypeSeverityChangedEventArgs e) {
            SetSeverityChanged();
        }

        private void Config_TypeSeverityChanged(object sender, LogTypeSeverityChangedEventArgs e) {
            SetSeverityChanged();
        }

        private void LogAction(LogEntry entry) {
            foreach (var item in Set) {
                item.Value.Log(entry);
            }
        }

        public LogServiceProxy this[string name] {
            get {
                lock (Set) {
                    if (Set.TryGetValue(name, out var value) == false) {
                        Set[name] = value = new LogServiceProxy(Factory.Create(name));
                        Set[name].Config.TypeSeverityChanged += Config_TypeSeverityChanged;
                        Set[name].Config.DefaultSeverityChanged += Config_DefaultSeverityChanged;
                    }
                    return value;
                }
            }
        }

        public bool Log(LogSeverity severity, Type type) {
            if (type == null) {
                return false;
            }
            if (severity == LogSeverity.None) {
                return false;
            }
            if (TypeSeverity.Count > 0) {
                if (TypeSeverity.TryGetValue(type.Name, out var value)) {
                    return value != LogSeverity.None && value <= severity;
                }
            }
            return DefaultSeverity != LogSeverity.None && DefaultSeverity <= severity;
        }

        public void Log(LogSeverity severity, Type type, object[] data) {
            if (data is null) return;
            Work.Add(new LogEntry(
                logType: type,
                logSeverity: severity,
                logList: data
                    .Select(d => $"{d}")
                    .ToArray()));
        }
    }
}
