using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.Text {
    public abstract class DecodedTextBuilder {
        private long Cursor;
        private object State;

        private async Task Sequence(object state, ReadOnlySequence<char> sequence, CancellationToken cancellationToken) {
            if (State != state) {
                State = state;
                Cursor = 0;
                await Clear(cancellationToken);
            }
            if (sequence.Length > Cursor) {
                var slice = sequence.Slice(Cursor);
                var length = slice.Length;
                if (length > 0) {
                    foreach (var memory in slice) {
                        await Add(memory, cancellationToken);
                    }
                    Cursor += length;
                }
            }
        }

        internal async Task Decode(DecodedText decoded, CancellationToken cancellationToken) {
            if (null == decoded) throw new ArgumentNullException(nameof(decoded));
            await Task.Run(
                cancellationToken: cancellationToken, 
                function: () => Sequence(decoded.State, decoded.Sequence, cancellationToken));
        }

        internal async Task Complete(DecodedText _, CancellationToken cancellationToken) {
            await Complete(cancellationToken);
        }

        protected abstract Task Clear(CancellationToken cancellationToken);
        protected abstract Task Add(ReadOnlyMemory<char> memory, CancellationToken cancellationToken);
        
        protected virtual Task Complete(CancellationToken cancellationToken) {
            return Task.CompletedTask;
        }
    }
}
