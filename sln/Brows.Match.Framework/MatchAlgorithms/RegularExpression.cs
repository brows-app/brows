using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.MatchAlgorithms {
    internal class RegularExpression : MatchAlgorithm {
        protected virtual Regex Convert(string pattern) {
            return new Regex(pattern, Options());
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

        protected sealed override async IAsyncEnumerable<T> Match<T>(string pattern, IEnumerable<T> source, Func<T, string> selector, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (null == pattern) throw new ArgumentNullException(nameof(pattern));
            if (null == source) throw new ArgumentNullException(nameof(source));
            if (null == selector) throw new ArgumentNullException(nameof(selector));

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
}
