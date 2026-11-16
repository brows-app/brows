using System;
using System.Collections.Generic;

namespace Brows.Commands;

internal sealed class FindMatch {
    private const int DefaultPreviewLength = 512;

    private string Preview { get; }

    private FindMatch(string text, int index, int length, int line, int lineIndex, int maxPreviewLength) {
        var indexIsInvalid = index < 0 || index > text.Length;
        if (indexIsInvalid) {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        var lengthIsInvalid = length < 0 || length > text.Length - index;
        if (lengthIsInvalid) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        if (line < 0) {
            throw new ArgumentOutOfRangeException(nameof(line));
        }
        if (lineIndex < 0) {
            throw new ArgumentOutOfRangeException(nameof(lineIndex));
        }
        if (maxPreviewLength < 0) {
            throw new ArgumentOutOfRangeException(nameof(maxPreviewLength));
        }

        Index = index;
        MatchLength = length;
        Line = line;
        LineIndex = lineIndex;
        var previewLength = Math.Min(length, maxPreviewLength);
        Preview = previewLength == 0 ? string.Empty : new string(text.AsSpan(index, previewLength));
    }

    private readonly record struct MatchSpan(int Index, int Length, int Order);

    internal static FindMatch Create(string text,
                                     int index,
                                     int length,
                                     int line,
                                     int lineIndex,
                                     int maxPreviewLength) {
        if (text is null) {
            throw new ArgumentNullException(nameof(text));
        }
        return new FindMatch(text, index, length, line, lineIndex, maxPreviewLength);
    }

    public int Index { get; }
    public int MatchLength { get; }
    public int TotalLength => Preview.Length;
    public int Line { get; }
    public int LineIndex { get; }

    public override string ToString() {
        return Preview;
    }

    public static IEnumerable<FindMatch> In(string text, IMatched matched, CancellationToken token = default) {
        return In(text, matched, DefaultPreviewLength, token);
    }

    public static IEnumerable<FindMatch> In(string text,
                                            IMatched matched,
                                            int maxPreviewLength,
                                            CancellationToken token = default) {
        if (text is null) {
            throw new ArgumentNullException(nameof(text));
        }
        if (matched is null) {
            throw new ArgumentNullException(nameof(matched));
        }
        if (maxPreviewLength < 0) {
            throw new ArgumentOutOfRangeException(nameof(maxPreviewLength));
        }

        var matches = matched.Matches;
        if (matches is null) {
            return Array.Empty<FindMatch>();
        }

        var spans = new List<MatchSpan>();
        var ordered = true;
        var previousIndex = -1;
        var order = 0;
        var checkedMatches = 0;
        foreach (var match in matches) {
            if ((checkedMatches++ & 0x3FF) == 0) {
                token.ThrowIfCancellationRequested();
            }
            if (match is null) {
                continue;
            }

            var index = match.Index;
            var length = match.Length;
            var spanIsInvalid = index < 0 || index > text.Length || length < 0 || length > text.Length - index;
            if (spanIsInvalid) {
                continue;
            }
            if (index < previousIndex) {
                ordered = false;
            }
            previousIndex = index;
            spans.Add(new MatchSpan(index, length, order++));
        }

        token.ThrowIfCancellationRequested();
        if (ordered == false) {
            spans.Sort(static (left, right) => {
                var indexOrder = left.Index.CompareTo(right.Index);
                if (indexOrder != 0) {
                    return indexOrder;
                }
                return left.Order.CompareTo(right.Order);
            });
            token.ThrowIfCancellationRequested();
        }
        if (spans.Count == 0) {
            return Array.Empty<FindMatch>();
        }

        var result = new List<FindMatch>(spans.Count);
        var line = 0;
        var lineStart = 0;
        var scan = 0;
        var checkedCharacters = 0;
        foreach (var span in spans) {
            while (scan < span.Index) {
                if ((checkedCharacters++ & 0x3FF) == 0) {
                    token.ThrowIfCancellationRequested();
                }

                var current = text[scan];
                var scanAtCarriageReturnLineFeed = current == '\r' &&
                    scan + 1 < text.Length &&
                    text[scan + 1] == '\n';
                if (scanAtCarriageReturnLineFeed) {
                    if (scan + 1 == span.Index) {
                        scan++;
                    }
                    else {
                        scan += 2;
                        line++;
                        lineStart = scan;
                    }
                }
                else if (current is '\r' or '\n') {
                    scan++;
                    line++;
                    lineStart = scan;
                }
                else {
                    scan++;
                }
            }

            token.ThrowIfCancellationRequested();
            result.Add(new FindMatch(text,
                                     span.Index,
                                     span.Length,
                                     line,
                                     span.Index - lineStart,
                                     maxPreviewLength));
        }

        return result;
    }
}
