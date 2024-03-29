﻿using System.Buffers;
using System.Collections.Generic;

namespace Domore.Buffers {
    internal class BufferPool {
        private readonly List<BufferRental> Buffers = new();

        public bool Clear { get; set; }

        public BufferSize RentSize {
            get => _RentSize ?? (_RentSize = new BufferSize());
            set => _RentSize = value;
        }
        private BufferSize _RentSize;

        public void Free() {
            lock (Buffers) {
                foreach (var buffer in Buffers) {
                    buffer.Free(Clear);
                }
                Buffers.Clear();
            }
        }

        public class Of<T> : BufferPool {
            public ArrayPool<T> Pool {
                get => _Pool ?? (_Pool = ArrayPool<T>.Shared);
                set => _Pool = value;
            }
            private ArrayPool<T> _Pool;

            public T[] Rent(int sizeHint = default) {
                var pool = Pool;
                var space = pool.Rent(sizeHint == default ? RentSize.Length : sizeHint);
                var rental = BufferRental.Keep(pool, space);
                lock (Buffers) {
                    Buffers.Add(rental);
                }
                return space;
            }
        }
    }

    internal sealed class BufferPool<T> : BufferPool.Of<T> {
    }
}
