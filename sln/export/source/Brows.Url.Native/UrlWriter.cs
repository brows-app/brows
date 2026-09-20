using System;
using System.Buffers;

namespace Brows;

internal sealed class UrlWriter : IBufferWriter<char> {
    void IBufferWriter<char>.Advance(int count) {
        throw new NotImplementedException();
    }

    Memory<char> IBufferWriter<char>.GetMemory(int sizeHint) {
        throw new NotImplementedException();
    }

    Span<char> IBufferWriter<char>.GetSpan(int sizeHint) {
        throw new NotImplementedException();
    }
}
