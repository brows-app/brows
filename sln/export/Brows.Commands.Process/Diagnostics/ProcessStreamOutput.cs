namespace Brows.Diagnostics {
    internal sealed class ProcessStreamOutput : ProcessStreamItem {
        public ProcessOutputKind Kind { get; }

        public ProcessStreamOutput(ProcessOutputKind kind) {
            Kind = kind;
        }
    }
}
