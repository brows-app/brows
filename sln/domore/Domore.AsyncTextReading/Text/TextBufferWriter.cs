using System;
using System.Buffers;

namespace Domore.Text {
    using Buffers;

    internal class TextBufferWriter : IBufferWriter<char> {
        private readonly SequenceUpdater<char> TextSequence = new();

        private int Cursor;
        private char[] Buffer;
        private SequenceSegment<char> StartSegment;
        private SequenceSegment<char> EndSegment;

        private char[] NewBuffer(int sizeHint) {
            Cursor = 0;
            Buffer = BufferPool.Rent(sizeHint);
            return Buffer;
        }

        public long Written { get; private set; }
        public BufferPool<char> BufferPool { get; }

        public ReadOnlySequence<char> Sequence =>
            TextSequence.Sequence;

        public TextBufferWriter(BufferPool<char> bufferPool) {
            BufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));
        }

        public void Complete() {
            TextSequence.Complete();
        }

        void IBufferWriter<char>.Advance(int count) {
            var memory = Buffer.AsMemory(Cursor, count);
            if (StartSegment == null) {
                StartSegment = EndSegment = new SequenceSegment<char>(memory);
            }
            else {
                EndSegment = EndSegment.Append(memory);
            }
            TextSequence.Update(StartSegment, EndSegment);
            Cursor += count;
            Written += count;
        }

        Memory<char> IBufferWriter<char>.GetMemory(int sizeHint) {
            if (Cursor < Buffer?.Length) {
                return Buffer.AsMemory(Cursor, Buffer.Length - Cursor);
            }
            return NewBuffer(sizeHint);
        }

        Span<char> IBufferWriter<char>.GetSpan(int sizeHint) {
            if (Cursor < Buffer?.Length) {
                return Buffer.AsSpan(Cursor, Buffer.Length - Cursor);
            }
            return NewBuffer(sizeHint);
        }
    }
}
