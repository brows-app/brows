using System;
using System.Text.RegularExpressions;

namespace Brows.MatchAlgorithms {
    internal sealed class RegularExpressionMatcher : Matcher {
        public Regex Regex { get; }

        public RegularExpressionMatcher(Regex regex) {
            Regex = regex ?? throw new ArgumentNullException(nameof(regex));
        }

        public sealed override bool Matches(string s) {
            return Regex.IsMatch(s);
        }
    }
}
