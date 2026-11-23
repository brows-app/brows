namespace Brows.Matching;

/// <summary>
/// Describes the position and length of a text match.
/// </summary>
public interface IMatch {
    /// <summary>
    /// Gets the zero-based position at which the match starts.
    /// </summary>
    int Index { get; }

    /// <summary>
    /// Gets the number of characters in the match.
    /// </summary>
    int Length { get; }
}
