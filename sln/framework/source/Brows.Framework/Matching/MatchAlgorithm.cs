using Brows.Matching.MatchAlgorithms;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Brows.Matching;

/// <summary>
/// Provides a base for pattern matching and creates the supported matching algorithms.
/// </summary>
public abstract class MatchAlgorithm : IMatchAlgorithm {
    private static MatchAlgorithm PrivateCreate(string kind) {
        var c = StringComparison.OrdinalIgnoreCase;
        var k = kind?.Trim() ?? "";
        if (k == "") k = nameof(Wildcard);
        if (k.Equals(nameof(Wildcard), c)) return new Wildcard();
        if (k.Equals(nameof(Regex), c)) return new RegularExpression();
        if (k.Equals(nameof(RegularExpression), c)) return new RegularExpression();
        throw new ArgumentException(paramName: nameof(kind), message: $"{nameof(kind)} [{kind}]");
    }

    /// <summary>
    /// Gets or sets whether matching ignores letter case.
    /// </summary>
    protected bool IgnoreCase { get; set; }

    /// <summary>
    /// Creates a matcher for a pattern.
    /// </summary>
    /// <param name="pattern">
    /// The pattern to match.
    /// </param>
    /// <returns>
    /// A matcher that tests text against the pattern.
    /// </returns>
    protected abstract IMatcher Matcher(string pattern);

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
    protected abstract IAsyncEnumerable<T> Match<T>(string pattern,
                                                    IEnumerable<T> source,
                                                    Func<T, string> selector,
                                                    CancellationToken cancellationToken);

    /// <summary>
    /// Creates a case-sensitive pattern matching algorithm.
    /// </summary>
    /// <param name="kind">
    /// Wildcard, Regex, or RegularExpression. Null or blank selects Wildcard.
    /// </param>
    /// <returns>
    /// The requested algorithm.
    /// </returns>
    public static IMatchAlgorithm Create(string kind = null) {
        return PrivateCreate(kind);
    }

    /// <summary>
    /// Creates a supported pattern matching algorithm.
    /// </summary>
    /// <param name="ignoreCase">
    /// Whether matching ignores letter case.
    /// </param>
    /// <param name="kind">
    /// Wildcard, Regex, or RegularExpression. Null or blank selects Wildcard.
    /// </param>
    /// <returns>
    /// The requested algorithm.
    /// </returns>
    public static IMatchAlgorithm Create(bool ignoreCase, string kind = null) {
        var
        instance = PrivateCreate(kind);
        instance.IgnoreCase = ignoreCase;
        return instance;
    }

    IMatcher IMatchAlgorithm.Matcher(string pattern) {
        return Matcher(pattern);
    }

    IAsyncEnumerable<T> IMatchAlgorithm.Match<T>(string pattern,
                                                 IEnumerable<T> source,
                                                 Func<T, string> selector,
                                                 CancellationToken cancellationToken) {
        return Match(pattern, source, selector, cancellationToken);
    }
}
