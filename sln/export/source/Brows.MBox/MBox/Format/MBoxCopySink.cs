using System;
using System.IO;

namespace Brows.MBox.Format;

/// <summary>
/// Copies the lines that are read to a stream, exactly as they are stored.
/// </summary>
internal sealed class MBoxCopySink : IMBoxLineSink {
    private readonly Stream _Destination;

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxCopySink"/> class.
    /// </summary>
    /// <param name="destination">
    /// The stream that receives the lines.
    /// </param>
    internal MBoxCopySink(Stream destination) {
        _Destination = destination ?? throw new ArgumentNullException(nameof(destination));
    }

    /// <summary>
    /// Appends the specified bytes to the line that is being read.
    /// </summary>
    /// <param name="bytes">
    /// The bytes to append.
    /// </param>
    public void Write(ReadOnlySpan<byte> bytes) {
        if (bytes.Length == 0) {
            return;
        }
        _Destination.Write(bytes);
    }

    /// <summary>
    /// Appends the specified byte to the line that is being read.
    /// </summary>
    /// <param name="value">
    /// The byte to append.
    /// </param>
    public void WriteByte(byte value) {
        _Destination.WriteByte(value);
    }

    /// <summary>
    /// Ends the line that was read, and appends its terminator.
    /// </summary>
    /// <param name="terminatorLength">
    /// The number of bytes of the terminator of the line.
    /// </param>
    public void EndLine(int terminatorLength) {
        if (terminatorLength == 2) {
            _Destination.WriteByte((byte)'\r');
            _Destination.WriteByte((byte)'\n');
        }
        else if (terminatorLength == 1) {
            _Destination.WriteByte((byte)'\n');
        }
    }
}
