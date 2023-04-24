using System.Text.RegularExpressions;

namespace Brows.MatchAlgorithms {
    internal sealed class Wildcard : RegularExpression {
        protected sealed override Regex Convert(string pattern) {
            var p = "^" + Regex
                .Escape(pattern)
                .Replace("\\?", ".")
                .Replace("\\*", ".*") + "$";
            return new Regex(p, Options());
        }
    }
}
