namespace Brows.Diagnostics {
    internal class ProcessConfig {
        public Process Default {
            get => _Default ??= new();
            set => _Default = value;
        }
        private Process _Default;

        public class Process {
            public string FileName { get; set; } = "cmd.exe";
            public string Arguments { get; set; } = "/k";
        }
    }
}
