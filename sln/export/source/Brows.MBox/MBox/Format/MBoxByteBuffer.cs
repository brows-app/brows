using System;
using System.Collections.Generic;

namespace Brows.MBox.Format;

/// <summary>
/// Collects the bytes of a stored body, so that they can be decoded later.
/// </summary>
/// <remarks>
/// The bytes are kept as they are stored, because the character set that decodes them is only
/// known once the transfer encoding has been undone.
/// </remarks>
internal sealed class MBoxByteBuffer {
    private byte[] _Buffer;
    private int _Length;

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxByteBuffer"/> class.
    /// </summary>
    internal MBoxByteBuffer() {
        _Buffer = new byte[1024];
    }

    /// <summary>
    /// Gets the number of bytes that were collected.
    /// </summary>
    internal int Length =>
        _Length;

    /// <summary>
    /// Appends the specified bytes.
    /// </summary>
    /// <param name="bytes">
    /// The bytes to append.
    /// </param>
    internal void Append(ReadOnlySpan<byte> bytes) {
        if (bytes.Length == 0) {
            return;
        }
        if (_Length + bytes.Length > _Buffer.Length) {
            var size = Math.Max(_Buffer.Length * 2, _Length + bytes.Length);
            Array.Resize(ref _Buffer, size);
        }
        bytes.CopyTo(_Buffer.AsSpan(_Length));
        _Length += bytes.Length;
    }

    /// <summary>
    /// Removes the first byte of the collected bytes.
    /// </summary>
    internal void RemoveFirst() {
        if (_Length == 0) {
            return;
        }
        Array.Copy(_Buffer, 1, _Buffer, 0, _Length - 1);
        _Length--;
    }

    /// <summary>
    /// Returns the collected bytes.
    /// </summary>
    /// <returns>
    /// A copy of the collected bytes.
    /// </returns>
    internal byte[] ToArray() {
        var bytes = new byte[_Length];
        Array.Copy(_Buffer, bytes, _Length);
        return bytes;
    }
}
