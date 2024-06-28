using Domore.Buffers;
using Domore.IO;
using Domore.Logs;
using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Domore.Text {
    internal sealed class TextDecoder {
        private static readonly ILog Log = Logging.For(typeof(TextDecoder));

        private readonly Encoding Encoding;
        private readonly TextBufferWriter BufferWriter;
        private readonly StreamTextReader StreamReader;
        private readonly Channel<DecodedText> Ch = Channel.CreateUnbounded<DecodedText>(new UnboundedChannelOptions {
            SingleReader = true,
            SingleWriter = true
        });

        private SequenceUpdatedEventArgs<byte> Event;

        private DecodedText Decoded() {
            return new DecodedText(this);
        }

        private TextDecoderStates Complete(TextDecoderStates state, Exception exception = null) {
            if (Log.Info()) {
                Log.Info($"{nameof(Complete)}[{EncodingName}][{state}]");
            }
            Event = null;
            Exception = exception;
            ByteSequence.Updated -= ByteSequence_Updated;
            BufferWriter.Complete();
            States = state | TextDecoderStates.Complete;
            Ch.Writer.TryWrite(Decoded());
            return States;
        }

        private TextDecoderStates Update() {
#if DEBUG
            if (Log.Debug()) {
                Log.Debug($"{nameof(Update)}[{EncodingName}]");
            }
#endif
            if (States.HasFlag(TextDecoderStates.Complete)) {
                return States;
            }
            lock (Ch) {
                if (States.HasFlag(TextDecoderStates.Complete)) {
                    return States;
                }
                if (CancellationToken.IsCancellationRequested) {
                    return Complete(TextDecoderStates.Canceled);
                }
                var e = Event;
                if (e == null) {
                    return States;
                }
                var sequence = e.Sequence;
                var sequenceComplete = e.Complete;
                var writer = BufferWriter;
                var written = writer.Written;
                try {
                    StreamReader.Decode(sequence, BufferWriter);
                }
                catch (Exception ex) {
#if DEBUG
                    if (Log.Debug()) {
                        Log.Debug($"{nameof(Exception)}[{EncodingName}]", ex);
                    }
#endif
                    return Complete(TextDecoderStates.Error, ex);
                }
                if (sequenceComplete) {
                    return Complete(TextDecoderStates.Success);
                }
                if (writer.Written > written) {
                    Ch.Writer.TryWrite(Decoded());
                }
                return States = TextDecoderStates.Running;
            }
        }

        private void ByteSequence_Updated(object sender, SequenceUpdatedEventArgs<byte> e) {
            Event = e;
            Task.Run(Update);
        }

        public ReadOnlySequence<char> TextSequence =>
            BufferWriter.Sequence;

        public long TextLength =>
            BufferWriter.Written;

        public string EncodingUsed =>
            StreamReader.EncodingName;

        public SequenceUpdater<byte> ByteSequence { get; }
        public Exception Exception { get; private set; }
        public TextDecoderStates States { get; private set; }
        public CancellationToken CancellationToken { get; set; }

        public string EncodingName { get; }
        public string ReplacementFallback { get; }
        public BufferPool<char> BufferPool { get; }

        public TextDecoder(string encodingName, string replacementFallback, SequenceUpdater<byte> byteSequence, BufferPool<char> bufferPool) {
            ByteSequence = byteSequence ?? throw new ArgumentNullException(nameof(byteSequence));
            ByteSequence.Updated += ByteSequence_Updated;
            BufferPool = bufferPool;
            BufferWriter = new TextBufferWriter(BufferPool);
            EncodingName = encodingName;
            ReplacementFallback = replacementFallback;
            Encoding = Encoding.GetEncoding(EncodingName, EncoderFallback.ExceptionFallback, ReplacementFallback == null
                ? new DecoderExceptionFallback()
                : new DecoderReplacementFallback(ReplacementFallback));
            StreamReader = new StreamTextReader(Encoding);
        }

        public async Task<DecodedText> Decode(CancellationToken cancellationToken) {
            var wait = await Ch.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
            if (wait == false) {
                return Decoded();
            }
            var read = Ch.Reader.TryRead(out var item);
            if (read == false) {
                return Decoded();
            }
            if (item.Complete) {
                lock (Ch) {
                    Ch.Writer.TryComplete();
                }
            }
            return item;
        }
    }
}
