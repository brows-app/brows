using System.Collections.Generic;

namespace Brows.Matching;

/// <summary>
/// Provides the text ranges found by a match.
/// </summary>
public interface IMatched {
    /// <summary>
    /// Gets the matching text ranges.
    /// </summary>
    IEnumerable<IMatch> Matches { get; }
}
