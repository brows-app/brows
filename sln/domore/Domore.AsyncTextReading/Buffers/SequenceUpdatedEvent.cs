using System;
using System.Buffers;

namespace Domore.Buffers {
    internal delegate void SequenceUpdatedEventHandler(object sender, SequenceUpdatedEventArgs e);
    internal delegate void SequenceUpdatedEventHandler<T>(object sender, SequenceUpdatedEventArgs<T> e);

    internal abstract class SequenceUpdatedEventArgs : EventArgs {
        protected SequenceUpdatedEventArgs(bool complete) {
            Complete = complete;
        }

        public bool Complete { get; }
    }

    internal sealed class SequenceUpdatedEventArgs<T> : SequenceUpdatedEventArgs {
        public ReadOnlySequence<T> Sequence { get; }

        public SequenceUpdatedEventArgs(bool complete, ReadOnlySequence<T> sequence) : base(complete) {
            Sequence = sequence;
        }
    }
}
