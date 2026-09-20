using Domore.Logs;

namespace Brows.CLI;

internal sealed class CliProgramConf {
    public string LogFileName { get; set; }
    public string LogFileDirectory { get; set; }
    public LogSeverity? LogFileThreshold { get; set; }
}
