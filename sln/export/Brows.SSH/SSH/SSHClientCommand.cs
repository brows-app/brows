using Domore.Buffers;
using Domore.Text;

namespace Brows.SSH {
    internal sealed class SSHClientCommand {
        private DecodedTextOptions DefaultTextOptions() {
            return new DecodedTextOptions {
                Encoding = ["utf-8"],
                StreamBuffer = new BufferOptions {
                    Clear = false,
                    Shared = true,
                    Size = 1024
                },
                TextBuffer = new BufferOptions {
                    Clear = false,
                    Shared = true,
                    Size = 100
                }
            };
        }

        public DecodedTextBuilder StdOutBuilder { get; set; }
        public DecodedTextBuilder StdErrBuilder { get; set; }

        public DecodedTextOptions StdOutText {
            get => _StdOutText ??= DefaultTextOptions();
            set => _StdOutText = value;
        }
        private DecodedTextOptions _StdOutText;

        public DecodedTextOptions StdErrText {
            get => _StdErrText ??= DefaultTextOptions();
            set => _StdErrText = value;
        }
        private DecodedTextOptions _StdErrText;

        public string Command { get; set; }
    }
}
