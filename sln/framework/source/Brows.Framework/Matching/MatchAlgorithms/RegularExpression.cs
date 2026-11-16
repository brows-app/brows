using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Brows.Matching.MatchAlgorithms;

internal class RegularExpression : MatchAlgorithm {
    /*
     * The pattern is user-supplied. A per-match timeout bounds the work of a
     * single matching call, which would otherwise run without interruption on
     * a catastrophically backtracking pattern, unreachable by cancellation.
     */
    protected static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    protected virtual Regex Convert(string pattern) {
        return new Regex(pattern, Options(), MatchTimeout);
    }

    protected RegexOptions Options() {
        return
            RegexOptions.Multiline |
            (IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
    }

    protected sealed override IMatcher Matcher(string pattern) {
        var regex = Convert(pattern);
        return new RegularExpressionMatcher(regex);
    }

    protected sealed override async IAsyncEnumerable<T>
    Match<T>(string pattern,
             IEnumerable<T> source,
             Func<T, string> selector,
             [EnumeratorCancellation] CancellationToken cancellationToken) {
        if (pattern is null) {
            throw new ArgumentNullException(nameof(pattern));
        }
        if (source is null) {
            throw new ArgumentNullException(nameof(source));
        }
        if (selector is null) {
            throw new ArgumentNullException(nameof(selector));
        }
        var matcher = Matcher(pattern);
        foreach (var item in source) {
            if (cancellationToken.IsCancellationRequested) {
                break;
            }
            var s = selector(item);
            if (matcher.Matches(s)) {
                yield return item;
            }
        }
        await Task.CompletedTask;
    }
}
