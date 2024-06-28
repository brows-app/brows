using System;
using System.Buffers;

namespace Domore.Buffers {
    internal abstract class SequenceUpdater {
    }

    internal sealed class SequenceUpdater<T> : SequenceUpdater {
        public event SequenceUpdatedEventHandler<T> Updated;

        public bool Completed { get; private set; }
        public ReadOnlySequence<T> Sequence { get; private set; }

        public void Update(SequenceSegment<T> startSegment, SequenceSegment<T> endSegment) {
            if (null == startSegment) throw new ArgumentNullException(nameof(startSegment));
            if (null == endSegment) throw new ArgumentNullException(nameof(endSegment));
            var sequence = Sequence = new ReadOnlySequence<T>(startSegment, startSegment.StartIndex, endSegment, endSegment.EndIndex);
            Updated?.Invoke(this, new(false, sequence));
        }

        public void Complete() {
            Completed = true;
            Updated?.Invoke(this, new(true, Sequence));
        }
    }
}
