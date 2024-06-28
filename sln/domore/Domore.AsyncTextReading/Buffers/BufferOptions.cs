using System.Buffers;

namespace Domore.Buffers {
    public sealed class BufferOptions {
        internal BufferPool<T> CreatePool<T>() {
            return new BufferPool<T> {
                Clear = Clear,
                Pool = Shared
                    ? ArrayPool<T>.Shared
                    : ArrayPool<T>.Create(),
                RentSize = new BufferSize {
                    Length = Size
                }
            };
        }

        public int Size { get; set; } = 512;
        public bool Clear { get; set; } = false;
        public bool Shared { get; set; } = true;
    }
}
