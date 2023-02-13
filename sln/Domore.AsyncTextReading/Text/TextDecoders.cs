using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.Text {
    using Buffers;
    using Logs;

    internal class TextDecoders {
        private static readonly ILog Log = Logging.For(typeof(TextDecoders));

        private List<TextDecoder> Encoders(SequenceUpdater<byte> byteSequence) {
            if (Log.Info()) {
                Log.Info($"{nameof(Encoders)}[{string.Join(",", Encoding)}]");
            }
            return Encoding
                .Select(e => new TextDecoder(
                    encodingName: e,
                    replacementFallback: EncodingFallback.TryGetValue(e, out var fallback)
                        ? fallback
                        : null,
                    byteSequence: byteSequence,
                    bufferPool: BufferPool))
                .ToList();
        }

        public DecodedTextDelegate Decoded { get; set; }
        public BufferPool<char> BufferPool { get; set; }

        public IReadOnlyList<string> Encoding {
            get => _Encoding ?? (_Encoding = new List<string>());
            set => _Encoding = value;
        }
        private IReadOnlyList<string> _Encoding;

        public IReadOnlyDictionary<string, string> EncodingFallback {
            get => _EncodingFallback ?? (_EncodingFallback = new Dictionary<string, string>());
            set => _EncodingFallback = value;
        }
        private IReadOnlyDictionary<string, string> _EncodingFallback;

        public async Task<DecodedText> Decode(SequenceUpdater<byte> byteSequence, CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(Decode));
            }
            var running = Encoders(byteSequence);
            var success = new List<DecodedText>(running.Count);
            var order = new List<string>(running.Select(decoder => decoder.EncodingName));
            for (; ; ) {
                if (running.Count == 0) {
                    if (success.Count == 0) {
                        if (Log.Info()) {
                            Log.Info($"{nameof(success.Count)}[{success.Count}]");
                        }
                        return null;
                    }
                    if (success.Count == 1) {
                        return success[0];
                    }
                    foreach (var encoding in order) {
                        foreach (var item in success) {
                            if (item.Decoder.EncodingName == encoding) {
                                return item;
                            }
                        }
                    }
                }
                var tasks = running.Select(d => d.Decode(cancellationToken));
                var task = await Task.WhenAny(tasks);
                var decoded = await task;
                if (decoded.Complete) {
                    if (Log.Info()) {
                        Log.Info($"{nameof(running.Remove)}[{decoded.Encoding}]");
                    }
                    running.Remove(decoded.Decoder);
                }
                if (decoded.Success) {
                    if (Log.Info()) {
                        Log.Info($"{nameof(TextDecoderStates.Success)}[{decoded.Encoding}]");
                    }
                    success.Add(decoded);
                }
                if (decoded.Error == false && decoded.Canceled == false) {
                    var handle = Decoded;
                    if (handle != null) {
                        await handle(decoded, cancellationToken);
                    }
                }
            }
        }
    }
}
