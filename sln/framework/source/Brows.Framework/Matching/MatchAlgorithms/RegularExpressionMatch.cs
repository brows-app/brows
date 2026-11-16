using System.Text.RegularExpressions;

namespace Brows.Matching.MatchAlgorithms;

internal readonly struct RegularExpressionMatch : IMatch {
    public readonly int Index => Agent.Index;
    public readonly int Length => Agent.Length;

    public readonly Match Agent { get; }

    public RegularExpressionMatch(Match agent) {
        Agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }
}
