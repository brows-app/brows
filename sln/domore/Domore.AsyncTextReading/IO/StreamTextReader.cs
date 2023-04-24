using System;
using System.Buffers;
using System.Text;

namespace Domore.IO {
    using Logs;
    using Text;

    internal class StreamTextReader {
        private static readonly ILog Log = Logging.For(typeof(StreamTextReader));

        private long BytesUsed;
        private long CharsUsed;
        private bool PreambleDetected;
        private StreamTextReader Agent;

        private bool PreambleRequired =>
            _PreambleRequired ?? (
            _PreambleRequired = Encoding.Preamble.Length > 0).Value;
        private bool? _PreambleRequired;

        private TextEncodingDetector EncodingDetector =>
            _EncodingDetector ?? (
            _EncodingDetector = new());
        private TextEncodingDetector _EncodingDetector;

        private Decoder Decoder =>
            _Decoder ?? (
            _Decoder = Encoding.GetDecoder());
        private Decoder _Decoder;

        public string EncodingName =>
            Agent == null
                ? Encoding.EncodingName
                : Agent.EncodingName;

        public Encoding Encoding { get; }

        public StreamTextReader(Encoding encoding) {
            Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        }

        public void Decode(in ReadOnlySequence<byte> sequence, IBufferWriter<char> writer) {
            if (Agent != null) {
                Agent.Decode(sequence, writer);
                return;
            }
            if (PreambleDetected == false) {
                if (PreambleRequired == true) {
                    var detected = PreambleDetected = EncodingDetector.TryDetect(sequence, out var encoding, out var preambleLength);
                    if (detected) {
                        if (encoding != null) {
                            if (encoding.CodePage == Encoding.CodePage) {
                                BytesUsed = preambleLength;
                            }
                            else {
                                Agent = new StreamTextReader(encoding) { PreambleDetected = true, BytesUsed = preambleLength };
                                Agent.Decode(sequence, writer);
                                return;
                            }
                        }
                    }
                }
            }
            if (sequence.Length > BytesUsed) {
                var slice = sequence.Slice(BytesUsed);
                if (slice.Length > 0) {
                    foreach (var memory in slice) {
                        if (memory.IsEmpty == false) {
                            var span = memory.Span;
                            var spanLength = span.Length;
                            Decoder.Convert(span, writer, flush: false, out var charsUsed, out var complete);
                            CharsUsed += charsUsed;
                            BytesUsed += spanLength;
                            if (Log.Info()) {
                                Log.Info($"{EncodingName} {nameof(BytesUsed)}[{BytesUsed}]{nameof(CharsUsed)}[{CharsUsed}]{nameof(complete)}[{complete}]");
                            }
                        }
                    }
                }
            }
        }
    }
}
