using System;
using System.Collections.Generic;

namespace Brows.Matching;

/// <summary>
/// Creates pattern matchers and filters collections by matching text.
/// </summary>
public interface IMatchAlgorithm {
    /// <summary>
    /// Creates a matcher for a pattern.
    /// </summary>
    /// <param name="pattern">
    /// The pattern to match.
    /// </param>
    /// <returns>
    /// A matcher that tests text against the pattern.
    /// </returns>
    IMatcher Matcher(string pattern);

    /// <summary>
    /// Enumerates the items whose text matches a pattern.
    /// </summary>
    /// <typeparam name="T">
    /// The type of item to test.
    /// </typeparam>
    /// <param name="pattern">
    /// The pattern to match.
    /// </param>
    /// <param name="source">
    /// The items to test.
    /// </param>
    /// <param name="selector">
    /// Selects the text to test for each item.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The matching items.
    /// </returns>
    IAsyncEnumerable<T> Match<T>(string pattern, IEnumerable<T> source, Func<T, string> selector, CancellationToken cancellationToken);
}
