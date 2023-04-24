using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Domore.IO {
    using Buffers;

    internal class StreamSequenceSegmenter {
        public BufferPool<byte> BufferPool { get; }

        public StreamSequenceSegmenter(BufferPool<byte> bufferPool) {
            BufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));
        }

        public async IAsyncEnumerable<SequenceSegment<byte>> Segments(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (null == stream) throw new ArgumentNullException(nameof(stream));
            var buffer = default(byte[]);
            var cursor = 0;
            var segment = default(SequenceSegment<byte>);
            for (; ; ) {
                if (cursor == buffer?.Length || buffer == null) {
                    cursor = 0;
                    buffer = BufferPool.Rent();
                }
                var bytesRead = await stream.ReadAsync(buffer, cursor, buffer.Length - cursor, cancellationToken);
                if (bytesRead == 0) {
                    break;
                }
                var mem = buffer.AsMemory(cursor, bytesRead);
                segment = segment?.Append(mem) ?? new SequenceSegment<byte>(mem);
                cursor += bytesRead;
                yield return segment;
            }
        }
    }
}
