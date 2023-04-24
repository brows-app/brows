using System;
using System.Runtime.CompilerServices;

namespace Domore.Logs {
    public sealed class Logging {
        private readonly static Logging Instance = new Logging();
        private readonly LogServiceFactory Factory = new LogServiceFactory();
        private readonly LogServiceCollection Services = new LogServiceCollection();

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal bool Log(Logger logger, LogSeverity severity) {
            return Services.Log(severity, logger?.Type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal void Log(Logger logger, LogSeverity severity, params object[] data) {
            Services.Log(severity, logger?.Type, data);
        }

        public static object Config =>
            new { Log = Instance.Services };

        public static ILog For(Type type) {
            return new Logger(type, Instance);
        }

        public static void Complete() {
            Instance.Services.Complete();
        }
    }
}
