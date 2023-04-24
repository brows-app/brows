using System;
using System.Buffers;

namespace Domore.Buffers {
    internal delegate void SequenceUpdatedEventHandler(object sender, SequenceUpdatedEventArgs e);
    internal delegate void SequenceUpdatedEventHandler<T>(object sender, SequenceUpdatedEventArgs<T> e);

    internal class SequenceUpdatedEventArgs : EventArgs {
        public bool Complete { get; }

        public SequenceUpdatedEventArgs(bool complete) {
            Complete = complete;
        }
    }

    internal class SequenceUpdatedEventArgs<T> : SequenceUpdatedEventArgs {
        public ReadOnlySequence<T> Sequence { get; }

        public SequenceUpdatedEventArgs(bool complete, ReadOnlySequence<T> sequence) : base(complete) {
            Sequence = sequence;
        }
    }
}
