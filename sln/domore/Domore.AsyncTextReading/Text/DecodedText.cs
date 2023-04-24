using System;
using System.Buffers;
using System.Text;

namespace Domore.Text {
    public class DecodedText {
        internal ReadOnlySequence<char> Sequence =>
            Decoder.TextSequence;

        internal TextDecoder Decoder { get; }

        internal DecodedText(TextDecoder decoder) {
            Decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        }

        public object State =>
            Decoder;

        public bool Running =>
            Decoder.States == TextDecoderStates.Running;

        public bool Error =>
            Decoder.States.HasFlag(TextDecoderStates.Error);

        public bool Canceled =>
            Decoder.States.HasFlag(TextDecoderStates.Canceled);

        public bool Complete =>
            Decoder.States.HasFlag(TextDecoderStates.Complete);

        public bool Success =>
            Decoder.States.HasFlag(TextDecoderStates.Success);

        public string Encoding =>
            Decoder.EncodingUsed;

        public long TextLength =>
            Decoder.TextLength;

        public string Text() {
            var builder = new StringBuilder();
            var sequence = Sequence;
            foreach (var memory in sequence) {
                builder.Append(memory);
            }
            return builder.ToString();
        }
    }
}
