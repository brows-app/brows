using System;

namespace Brows.MBox.Format;

/// <summary>
/// Receives the content of the lines that are read, together with their terminators.
/// </summary>
/// <remarks>
/// The sink is written while the line is read, so a caller that needs to transform the content
/// of a line can do so without holding the whole line in memory.
/// </remarks>
internal interface IMBoxLineSink {
    /// <summary>
    /// Appends the specified bytes to the line that is being read.
    /// </summary>
    /// <param name="bytes">
    /// The bytes to append.
    /// </param>
    void Write(ReadOnlySpan<byte> bytes);

    /// <summary>
    /// Appends the specified byte to the line that is being read.
    /// </summary>
    /// <param name="value">
    /// The byte to append.
    /// </param>
    void WriteByte(byte value);

    /// <summary>
    /// Ends the line that was read, and appends its terminator.
    /// </summary>
    /// <param name="terminatorLength">
    /// The number of bytes of the terminator of the line, which is zero when the line was not
    /// terminated.
    /// </param>
    void EndLine(int terminatorLength);
}
