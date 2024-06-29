using System;
using System.Text.RegularExpressions;

namespace Brows.MatchAlgorithms {
    internal sealed class Wildcard : RegularExpression {
        protected sealed override Regex Convert(string pattern) {
            ArgumentNullException.ThrowIfNull(pattern);
            if (pattern.Contains('*') == false && pattern.Contains('?') == false) {
                pattern = $"*{pattern}*";
            }
            var p = "^" + Regex
                .Escape(pattern)
                .Replace("\\?", ".")
                .Replace("\\*", ".*") + "$";
            return new Regex(p, Options());
        }
    }
}
