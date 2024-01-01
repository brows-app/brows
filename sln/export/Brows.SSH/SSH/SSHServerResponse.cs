using Domore.Text;

namespace Brows.SSH {
    internal sealed class SSHServerResponse {
        public DecodedText StdOut { get; }
        public DecodedText StdErr { get; }

        public SSHServerResponse(DecodedText stdOut, DecodedText stdErr) {
            StdOut = stdOut;
            StdErr = stdErr;
        }
    }
}
