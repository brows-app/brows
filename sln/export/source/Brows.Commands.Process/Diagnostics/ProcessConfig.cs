namespace Brows.Diagnostics;

internal sealed class ProcessConfig {
    public Process Default {
        get => field ??= new();
        set;
    }

    public sealed class Process {
        public string FileName { get; set; } = "cmd.exe";
        public string Arguments { get; set; } = "/k";
    }
}
