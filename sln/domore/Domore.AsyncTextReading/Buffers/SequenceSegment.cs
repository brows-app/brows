using System;
using System.Buffers;

namespace Domore.Buffers {
    internal sealed class SequenceSegment<T> : ReadOnlySequenceSegment<T> {
        private SequenceSegment() {
        }

        public int StartIndex => 0;
        public int EndIndex => Memory.Length;

        public SequenceSegment(ReadOnlyMemory<T> memory) {
            Memory = memory;
            RunningIndex = 0;
        }

        public SequenceSegment<T> Append(ReadOnlyMemory<T> memory) {
            var segment = new SequenceSegment<T> {
                Memory = memory,
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = segment;
            return segment;
        }
    }
}
