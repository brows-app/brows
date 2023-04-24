using System;
using System.Text.RegularExpressions;

namespace Brows.MatchAlgorithms {
    internal struct RegularExpressionMatch : IMatch {
        public int Index => Agent.Index;
        public int Length => Agent.Length;

        public Match Agent { get; }

        public RegularExpressionMatch(Match agent) {
            Agent = agent ?? throw new ArgumentNullException(nameof(agent));
        }
    }
}
