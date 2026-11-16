using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Brows.Commands;

internal sealed class FindMatcher : IMatcher {
    private static readonly TimeSpan MatcherTimeout = TimeSpan.FromSeconds(2);

    private Regex PatternRegex { get; }

    private static string RegexEscape(string pattern) {
        return Regex.Escape(pattern)
            .Replace("\\?", ".")
            .Replace("\\*", ".*");
    }

    private readonly struct FindRange : IMatch {
        public int Index { get; }
        public int Length { get; }

        public FindRange(int index, int length) {
            Index = index;
            Length = length;
        }
    }

    private sealed class FindMatches : IMatched {
        private IReadOnlyList<IMatch> MatchesList { get; }

        public IEnumerable<IMatch> Matches => MatchesList;

        public FindMatches(IReadOnlyList<IMatch> matches) {
            MatchesList = matches ?? throw new ArgumentNullException(nameof(matches));
        }
    }

    internal FindMatcher(string pattern, bool ignoreCase) : this(pattern, ignoreCase, MatcherTimeout) {
    }

    internal FindMatcher(string pattern, bool ignoreCase, TimeSpan timeout) {
        if (pattern is null) {
            throw new ArgumentNullException(nameof(pattern));
        }
        var patternHasWildcard = pattern.Contains('*') || pattern.Contains('?');
        if (patternHasWildcard == false) {
            pattern = $"*{pattern}*";
        }

        var expression = "\\A" + RegexEscape(pattern) + "\\z";
        var options = RegexOptions.NonBacktracking | RegexOptions.Singleline;
        if (ignoreCase) {
            options |= RegexOptions.IgnoreCase;
        }
        PatternRegex = new Regex(expression, options, timeout);
    }

    internal IEnumerable<FindMatch> FindInText(string text,
                                               int maxPreviewLength,
                                               CancellationToken token) {
        if (text is null) {
            throw new ArgumentNullException(nameof(text));
        }
        if (maxPreviewLength < 0) {
            throw new ArgumentOutOfRangeException(nameof(maxPreviewLength));
        }

        var lineStart = 0;
        var lineNumber = 0;
        while (lineStart <= text.Length) {
            token.ThrowIfCancellationRequested();

            var lineEnd = lineStart;
            var lineHasMoreCharacters = lineEnd < text.Length && text[lineEnd] != '\r' && text[lineEnd] != '\n';
            while (lineHasMoreCharacters) {
                if ((lineEnd & 0x3FF) == 0) {
                    token.ThrowIfCancellationRequested();
                }
                lineEnd++;
                lineHasMoreCharacters = lineEnd < text.Length && text[lineEnd] != '\r' && text[lineEnd] != '\n';
            }

            var lineLength = lineEnd - lineStart;
            var matches = PatternRegex.IsMatch(text.AsSpan(lineStart, lineLength));
            token.ThrowIfCancellationRequested();
            if (matches) {
                yield return FindMatch.Create(text,
                                              lineStart,
                                              lineLength,
                                              lineNumber,
                                              0,
                                              maxPreviewLength);
            }

            if (lineEnd == text.Length) {
                yield break;
            }

            var lineEndsWithCarriageReturnLineFeed = text[lineEnd] == '\r' &&
                lineEnd + 1 < text.Length &&
                text[lineEnd + 1] == '\n';
            lineStart = lineEndsWithCarriageReturnLineFeed ? lineEnd + 2 : lineEnd + 1;
            lineNumber++;
        }
    }

    public bool Matches(string text) {
        if (text is null) {
            throw new ArgumentNullException(nameof(text));
        }
        return PatternRegex.IsMatch(text);
    }

    public bool Matches(string text, out IMatched matched) {
        if (text is null) {
            throw new ArgumentNullException(nameof(text));
        }

        var collection = PatternRegex.Matches(text);
        var matches = new List<IMatch>(collection.Count);
        foreach (Match match in collection) {
            matches.Add(new FindRange(match.Index, match.Length));
        }
        matched = new FindMatches(matches);
        return matches.Count > 0;
    }
}
