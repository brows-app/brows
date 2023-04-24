namespace Brows.Diagnostics {
    internal class ProcessConfig {
        public Process Default {
            get => _Default ?? (_Default = new Process());
            set => _Default = value;
        }
        private Process _Default;

        public class Process {
            public string FileName { get; set; }
            public string Arguments { get; set; }
        }
    }
}
