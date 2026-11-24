using Domore.Logs;

namespace Brows.Agent;

internal sealed record AgentLogEntry(long Seq, DateTime Time, LogSeverity Severity, string Name, string Text) {
    internal bool IsError =>
        Severity >= LogSeverity.Error;

    internal string Format() {
        return $"{Seq} {Time:HH:mm:ss.fff} {Severity} {Name}: {Text}";
    }
}
