namespace Brows.SSH {
    internal sealed class SSHClientOutput {
        public string Content { get; }
        public SSHClientOutputKind Kind { get; }

        public SSHClientOutput(string content, SSHClientOutputKind kind) {
            Content = content;
            Kind = kind;
        }
    }
}
