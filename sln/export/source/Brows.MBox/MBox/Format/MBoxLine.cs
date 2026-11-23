namespace Brows.MBox.Format;

/// <summary>
/// Describes a line read from a mailbox.
/// </summary>
internal readonly struct MBoxLine {
    /// <summary>
    /// Gets the absolute offset, from the start of the source, of the first byte of the line.
    /// </summary>
    internal long Offset { get; init; }

    /// <summary>
    /// Gets the number of bytes of the line, excluding its terminator.
    /// </summary>
    internal int Length { get; init; }

    /// <summary>
    /// Gets the number of bytes of the line that were kept.
    /// </summary>
    internal int Kept { get; init; }

    /// <summary>
    /// Gets the number of bytes of the line terminator.
    /// </summary>
    internal int TerminatorLength { get; init; }

    internal bool IsComplete { get; init; }

    internal bool EndsWithCarriageReturn { get; init; }

    /// <summary>
    /// Gets the absolute offset, from the start of the source, at which the line ends.
    /// </summary>
    internal long End =>
        Offset + Length + TerminatorLength;
}
