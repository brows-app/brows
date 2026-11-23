namespace Brows.Matching;

/// <summary>
/// Tests text against a pattern.
/// </summary>
public interface IMatcher {
    /// <summary>
    /// Determines whether text matches the pattern.
    /// </summary>
    /// <param name="s">
    /// The text to test.
    /// </param>
    /// <returns>
    /// True if the text matches; otherwise, false.
    /// </returns>
    bool Matches(string s);

    /// <summary>
    /// Determines whether text matches the pattern and supplies the matching ranges.
    /// </summary>
    /// <param name="s">
    /// The text to test.
    /// </param>
    /// <param name="matched">
    /// Receives the matching ranges when a match is found.
    /// </param>
    /// <returns>
    /// True if the text matches; otherwise, false.
    /// </returns>
    bool Matches(string s, out IMatched matched);
}
