using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
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
                await Clear(cancellationToken).ConfigureAwait(false);
            }
            if (sequence.Length > Cursor) {
                var slice = sequence.Slice(Cursor);
                var length = slice.Length;
                if (length > 0) {
                    foreach (var memory in slice) {
                        await Add(memory, cancellationToken).ConfigureAwait(false);
                    }
                    Cursor += length;
                }
            }
        }

        internal async Task Decode(DecodedText decoded, CancellationToken cancellationToken) {
            if (null == decoded) throw new ArgumentNullException(nameof(decoded));
            await Task
                .Run(
                    cancellationToken: cancellationToken,
                    function: () => Sequence(decoded.State, decoded.Sequence, cancellationToken))
                .ConfigureAwait(false);
        }

        internal Task Complete(DecodedText _, CancellationToken cancellationToken) {
            return Complete(cancellationToken);
        }

        internal static DecodedTextBuilder Combine(IEnumerable<DecodedTextBuilder> builders) {
            return new Aggregate(builders);
        }

        protected abstract Task Clear(CancellationToken cancellationToken);
        protected abstract Task Add(ReadOnlyMemory<char> memory, CancellationToken cancellationToken);

        protected virtual Task Complete(CancellationToken cancellationToken) {
            return Task.CompletedTask;
        }

        private sealed class Aggregate : DecodedTextBuilder {
            private Task All(Func<DecodedTextBuilder, CancellationToken, Task> task, CancellationToken cancellationToken) {
                if (null == task) throw new ArgumentNullException(nameof(task));
                return Task.WhenAll(List.Select(item => task(item, cancellationToken)));
            }

            protected sealed override Task Clear(CancellationToken cancellationToken) {
                return All((i, ct) => i.Clear(ct), cancellationToken);
            }

            protected sealed override Task Add(ReadOnlyMemory<char> memory, CancellationToken cancellationToken) {
                return All((i, ct) => i.Add(memory, ct), cancellationToken);
            }

            protected sealed override Task Complete(CancellationToken cancellationToken) {
                return All((i, ct) => i.Complete(ct), cancellationToken);
            }

            public IReadOnlyList<DecodedTextBuilder> List { get; }

            public Aggregate(IEnumerable<DecodedTextBuilder> items) {
                if (null == items) throw new ArgumentNullException(nameof(items));
                List = items.Where(item => item != null).ToList();
            }
        }
    }
}
