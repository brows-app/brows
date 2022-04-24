using System;

namespace Brows.Logger {

    public sealed class Logging {
        private static Logging Instance { get; } = new Logging();
        private readonly LogServiceCollection Services = new LogServiceCollection();

        private Logging() {
        }

        internal bool Log(Logger logger, LogSeverity severity) {
            return Services.Log(severity, logger?.Type);
        }

        internal void Log(Logger logger, LogSeverity severity, params object[] data) {
            Services.Log(severity, logger?.Type, data);
        }

        public static ILog For(Type type) {
            return new Logger(type, Instance);
        }

        public static void Add(ILogService service) {
            Instance.Services.Add(service);
        }
    }
}
