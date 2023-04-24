using System;
using System.Runtime.CompilerServices;

namespace Domore.Logs {
    internal sealed class Logger : ILog {
        public Type Type { get; }
        public Logging Logging { get; }

        public Logger(Type type, Logging logging) {
            Type = type;
            Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool Enabled(LogSeverity severity) {
            return Logging.Log(this, severity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void Data(LogSeverity severity, params object[] data) {
            Logging.Log(this, severity, data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool Debug() {
            return Enabled(LogSeverity.Debug);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void Debug(params object[] data) {
            Data(LogSeverity.Debug, data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool Info() {
            return Enabled(LogSeverity.Info);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void Info(params object[] data) {
            Data(LogSeverity.Info, data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool Warn() {
            return Enabled(LogSeverity.Warn);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void Warn(params object[] data) {
            Data(LogSeverity.Warn, data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool Error() {
            return Enabled(LogSeverity.Error);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void Error(params object[] data) {
            Data(LogSeverity.Error, data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool Critical() {
            return Enabled(LogSeverity.Critical);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void Critical(params object[] data) {
            Data(LogSeverity.Critical, data);
        }
    }
}
