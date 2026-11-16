namespace Brows.MBox.Format;

/// <summary>
/// Describes a line read from a mailbox.
/// </summary>
internal readonly struct MBoxLine {
    /// <summary>
    /// Gets the absolute offset, from the start of the source, of the first byte of the line.
    /// </summary>
    public long Offset { get; init; }

    /// <summary>
    /// Gets the number of bytes of the line, excluding its terminator.
    /// </summary>
    public int Length { get; init; }

    /// <summary>
    /// Gets the number of bytes of the line that were kept.
    /// </summary>
    public int Kept { get; init; }

    /// <summary>
    /// Gets the number of bytes of the line terminator.
    /// </summary>
    public int TerminatorLength { get; init; }

    public bool IsComplete { get; init; }

    public bool EndsWithCarriageReturn { get; init; }

    /// <summary>
    /// Gets the absolute offset, from the start of the source, at which the line ends.
    /// </summary>
    public long End =>
        Offset + Length + TerminatorLength;
}
