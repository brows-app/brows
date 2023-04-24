using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Brows.Commands {
    internal sealed class FindMatch {
        private readonly StringBuilder StringBuilder = new();
        private bool Building = true;

        private IMatch Match { get; }

        private FindMatch(IMatch match) {
            Match = match ?? throw new ArgumentNullException(nameof(match));
        }

        public int MatchLength => Match.Length;
        public int TotalLength => StringBuilder.Length;
        public int Line { get; private set; }
        public int LineIndex { get; private set; }

        public sealed override string ToString() {
            return StringBuilder.ToString();
        }

        public static IEnumerable<FindMatch> In(string s, IMatched matched) {
            if (null == s) throw new ArgumentNullException(nameof(s));
            if (null == matched) throw new ArgumentNullException(nameof(matched));
            var matches = matched.Matches;
            if (matches == null) {
                return Array.Empty<FindMatch>();
            }
            var lookup = matches.ToLookup(m => m.Index, m => new FindMatch(m));
            var building = new List<FindMatch>();
            var line = 0;
            var lineIndex = 0;
            var pad = 0;
            for (var i = -pad; i < s.Length; i++) {
                foreach (var group in lookup.Where(group => group.Key - pad == i)) {
                    foreach (var findMatch in group) {
                        findMatch.Line = line;
                        findMatch.LineIndex = lineIndex;
                        building.Add(findMatch);
                    }
                }
                if (i < 0) {
                    continue;
                }
                var c = s[i];
                if (c == '\n') {
                    line++;
                    lineIndex = 0;
                }
                foreach (var b in building) {
                    b.StringBuilder.Append(c);
                    if (b.StringBuilder.Length > b.MatchLength + pad) {
                        b.Building = false;
                    }
                }
                building.RemoveAll(b => b.Building == false);
                lineIndex++;
            }
            return lookup.SelectMany(m => m);
        }
    }
}
