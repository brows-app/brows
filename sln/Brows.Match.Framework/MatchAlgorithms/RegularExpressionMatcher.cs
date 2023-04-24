using System;
using System.Text.RegularExpressions;

namespace Brows.MatchAlgorithms {
    internal sealed class RegularExpressionMatcher : IMatcher {
        public Regex Regex { get; }

        public RegularExpressionMatcher(Regex regex) {
            Regex = regex ?? throw new ArgumentNullException(nameof(regex));
        }

        public bool Matches(string s) {
            return Regex.IsMatch(s);
        }

        public bool Matches(string s, out IMatched matched) {
            var collection = Regex.Matches(s);
            matched = new RegularExpressionMatched(collection);
            return collection.Count > 0;
        }
    }
}
