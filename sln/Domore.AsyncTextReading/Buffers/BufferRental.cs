using System;
using System.Buffers;

namespace Domore.Buffers {
    internal abstract class BufferRental {
        public abstract void Free(bool clear);

        public static BufferRental Keep<T>(ArrayPool<T> pool, T[] space) {
            return new Of<T>(pool, space);
        }

        private sealed class Of<T> : BufferRental {
            public ArrayPool<T> Pool { get; }
            public T[] Space { get; }

            public Of(ArrayPool<T> pool, T[] space) {
                Pool = pool ?? throw new ArgumentNullException(nameof(pool));
                Space = space;
            }

            public sealed override void Free(bool clear) {
                Pool.Return(Space, clear);
            }
        }
    }
}
