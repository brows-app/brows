using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.IO {
    using Buffers;
    using Text;

    internal class StreamTextDecoder {
        public DecodedTextDelegate Decoded { get; set; }
        public BufferPool<char> TextBuffer { get; set; }
        public BufferPool<byte> StreamBuffer { get; set; }
        public IReadOnlyList<string> Encoding { get; set; }
        public IReadOnlyDictionary<string, string> EncodingFallback { get; set; }

        public async Task<DecodedText> Decode(Stream stream, CancellationToken cancellationToken) {
            var encoders = new TextDecoders {
                Decoded = Decoded,
                Encoding = Encoding,
                EncodingFallback = EncodingFallback,
                BufferPool = TextBuffer
            };
            var sequence = new SequenceUpdater<byte>();
            var decode = encoders.Decode(sequence, cancellationToken);
            var read = new StreamSequenceSegmenter(StreamBuffer);
            var segments = read.Segments(stream, cancellationToken);
            var segment1 = default(SequenceSegment<byte>);
            await foreach (var segment in segments) {
                if (decode.IsCompleted) {
                    break;
                }
                segment1 = segment1 ?? segment;
                sequence.Update(segment1, segment);
            }
            sequence.Complete();
            return await decode;
        }
    }
}
