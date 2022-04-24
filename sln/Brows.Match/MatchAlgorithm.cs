using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace Brows {
    using MatchAlgorithms;

    public abstract class MatchAlgorithm {
        protected bool IgnoreCase { get; set; }

        public abstract Matcher Matcher(string pattern);
        public abstract IAsyncEnumerable<T> Match<T>(string pattern, IEnumerable<T> source, Func<T, string> selector, CancellationToken cancellationToken);

        public static MatchAlgorithm Create(string kind = null) {
            var c = StringComparison.OrdinalIgnoreCase;
            var k = kind?.Trim() ?? "";
            if (k == "") k = nameof(Wildcard);
            if (k.Equals(nameof(Wildcard), c)) return new Wildcard();
            if (k.Equals(nameof(Regex), c)) return new RegularExpression();
            if (k.Equals(nameof(RegularExpression), c)) return new RegularExpression();
            throw new ArgumentException(paramName: nameof(kind), message: $"{nameof(kind)} [{kind}]");
        }

        public static MatchAlgorithm Create(bool ignoreCase, string kind = null) {
            var
            instance = Create(kind);
            instance.IgnoreCase = ignoreCase;
            return instance;
        }
    }
}
