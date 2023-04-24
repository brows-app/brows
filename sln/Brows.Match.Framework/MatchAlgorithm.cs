using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace Brows {
    using MatchAlgorithms;

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

        protected bool IgnoreCase { get; set; }

        protected abstract IMatcher Matcher(string pattern);
        protected abstract IAsyncEnumerable<T> Match<T>(string pattern, IEnumerable<T> source, Func<T, string> selector, CancellationToken cancellationToken);

        public static IMatchAlgorithm Create(string kind = null) {
            return PrivateCreate(kind);
        }

        public static IMatchAlgorithm Create(bool ignoreCase, string kind = null) {
            var
            instance = PrivateCreate(kind);
            instance.IgnoreCase = ignoreCase;
            return instance;
        }

        IMatcher IMatchAlgorithm.Matcher(string pattern) {
            return Matcher(pattern);
        }

        IAsyncEnumerable<T> IMatchAlgorithm.Match<T>(string pattern, IEnumerable<T> source, Func<T, string> selector, CancellationToken cancellationToken) {
            return Match(pattern, source, selector, cancellationToken);
        }
    }
}
