using Brows.Matching;
using Brows.Support;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Brows.Commands;

[TestFixture]
internal sealed class FindMatchTest {
    private static IMatched Matched(params (int Index, int Length)[] spans) {
        var matches = spans
            .Select(span => (IMatch)new TestMatch(span.Index, span.Length))
            .ToArray();
        return new TestMatched(matches);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<string> Text, FoundInFileText Row) CreateLargeResult(
        FileInfo file,
        DirectoryInfo root) {
        var text = File.ReadAllText(file.FullName);
        var reference = new WeakReference<string>(text);
        var matcher = new FindMatcher("*needle", ignoreCase: false);
        matcher.Matches(text, out var matched);
        var match = FindMatch.In(text, matched, maxPreviewLength: 32).Single();
        return (reference, new FoundInFileText(file, root, match));
    }

    [Test]
    public void In_ExtractsCoordinatesAcrossLineEndingsAndAdjacentMatches() {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("coordinates.txt", "x\nab\r\nc\rd");
        var text = File.ReadAllText(file.FullName);
        var matches = FindMatch.In(text, Matched(
            (0, 1),
            (1, 0),
            (2, 1),
            (2, 1),
            (3, 1),
            (8, 1),
            (9, 0))).ToArray();

        Assert.That(matches.Select(match => (match.Index, match.Line, match.LineIndex)), Is.EqualTo(new[] {
            (0, 0, 0),
            (1, 0, 1),
            (2, 1, 0),
            (2, 1, 0),
            (3, 1, 1),
            (8, 3, 0),
            (9, 3, 1)
        }));
    }

    [Test]
    public void In_CopiesOnlyTheRequestedPreviewLength() {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("preview.txt", "0123456789");
        var text = File.ReadAllText(file.FullName);
        var matches = FindMatch.In(text, Matched((0, 10), (10, 0)), maxPreviewLength: 3).ToArray();

        Assert.That(matches[0].MatchLength, Is.EqualTo(10));
        Assert.That(matches[0].TotalLength, Is.EqualTo(3));
        Assert.That(matches[0].ToString(), Is.EqualTo("012"));
        Assert.That(matches[1].Index, Is.EqualTo(10));
        Assert.That(matches[1].LineIndex, Is.EqualTo(10));
        Assert.That(matches[1].ToString(), Is.Empty);
    }

    [Test]
    public void ResultRow_DoesNotRetainTheDecodedFileText() {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("large.txt", new string('a', 200_000) + "needle");
        var (weakText, row) = CreateLargeResult(file, fixture.Root);

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

        Assert.That(weakText.TryGetTarget(out _), Is.False);
        Assert.That(row.Match.TotalLength, Is.EqualTo(32));
        Assert.That(row.LineText, Has.Length.EqualTo(32));
        GC.KeepAlive(row);
    }

    private sealed class TestMatched : IMatched {
        private IReadOnlyList<IMatch> MatchesList { get; }

        public IEnumerable<IMatch> Matches => MatchesList;

        public TestMatched(IReadOnlyList<IMatch> matches) {
            MatchesList = matches;
        }
    }

    private readonly record struct TestMatch(int Index, int Length) : IMatch;
}
