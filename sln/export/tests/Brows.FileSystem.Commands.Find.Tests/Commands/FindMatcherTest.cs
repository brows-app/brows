using Brows.Localization;
using Brows.Support;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace Brows.Commands;

[TestFixture]
internal sealed class FindMatcherTest {
    [TestCase("abc\nabc", 2)]
    [TestCase("abc\r\nabc\r\n", 2)]
    [TestCase("abc", 1)]
    public void FindInText_MatchesEndAnchoredPatternAcrossNormalLineEndings(string contents, int expectedCount) {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("lines.txt", contents);
        var text = File.ReadAllText(file.FullName);
        var matches = new FindMatcher("*abc", ignoreCase: false)
            .FindInText(text, maxPreviewLength: 512, CancellationToken.None)
            .ToArray();

        Assert.That(matches, Has.Length.EqualTo(expectedCount));
        Assert.That(matches.Select(match => match.Line), Is.EqualTo(Enumerable.Range(0, expectedCount)));
        Assert.That(matches.All(match => match.LineIndex == 0), Is.True);
        Assert.That(matches.All(match => match.ToString() == "abc"), Is.True);
    }

    [Test]
    public void FindInText_TreatsBareCarriageReturnAsALineEnding() {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("bare-cr.txt", "abc\rdef");
        var text = File.ReadAllText(file.FullName);
        var matches = new FindMatcher("*abc", ignoreCase: false)
            .FindInText(text, maxPreviewLength: 512, CancellationToken.None)
            .ToArray();

        Assert.That(matches, Has.Length.EqualTo(1));
        Assert.That(matches[0].Line, Is.Zero);
        Assert.That(matches[0].ToString(), Is.EqualTo("abc"));
    }

    [Test]
    public void FindInText_AppliesQuestionMarkToExactlyOneCharacter() {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("question.txt", "abc\nxabc\nabcdef");
        var text = File.ReadAllText(file.FullName);
        var matches = new FindMatcher("?bc", ignoreCase: false)
            .FindInText(text, maxPreviewLength: 512, CancellationToken.None)
            .ToArray();

        Assert.That(matches, Has.Length.EqualTo(1));
        Assert.That(matches[0].Line, Is.Zero);
        Assert.That(matches[0].ToString(), Is.EqualTo("abc"));
    }

    [Test]
    public void FindInText_HandlesZeroWidthMatchAtEofAfterTrailingNewline() {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("empty-line.txt", "abc\n");
        var text = File.ReadAllText(file.FullName);
        var matches = new FindMatcher("*", ignoreCase: false)
            .FindInText(text, maxPreviewLength: 512, CancellationToken.None)
            .ToArray();

        Assert.That(matches, Has.Length.EqualTo(2));
        Assert.That(matches[0].Index, Is.Zero);
        Assert.That(matches[0].Line, Is.Zero);
        Assert.That(matches[1].Index, Is.EqualTo(text.Length));
        Assert.That(matches[1].MatchLength, Is.Zero);
        Assert.That(matches[1].Line, Is.EqualTo(1));
        Assert.That(matches[1].LineIndex, Is.Zero);
    }

    [Test]
    public void FoundInFileText_DisplaysOneBasedLineNumber() {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("line-label.txt", "one\ntwo");
        var text = File.ReadAllText(file.FullName);
        var match = new FindMatcher("two", ignoreCase: false)
            .FindInText(text, maxPreviewLength: 512, CancellationToken.None)
            .Single();
        var row = new FoundInFileText(file, fixture.Root, match);
        var previousTranslation = Translation.Global;
        Translation.Global = new TestTranslation();
        try {
            Assert.That(row.Match.Line, Is.EqualTo(1));
            Assert.That(row.LineLabel, Is.EqualTo("Line 2"));
        }
        finally {
            Translation.Global = previousTranslation;
        }
    }

    [Test]
    public void FindInText_ThrowsWhenCancellationArrivesBeforeMatching() {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("cancel.txt", "abc\n");
        var text = File.ReadAllText(file.FullName);
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.Throws<OperationCanceledException>(() => new FindMatcher("*", ignoreCase: false)
            .FindInText(text, maxPreviewLength: 512, source.Token)
            .ToArray());
    }

    [Test]
    public void Matches_UsesBoundedNonBacktrackingWildcardBehavior() {
        var matcher = new FindMatcher("*a*a*a*a*a*a*a*a*b", ignoreCase: false);
        var text = new string('a', 120);

        Assert.That(matcher.Matches(text), Is.False);
    }

    [Test]
    public void Matches_PreservesWrappingAndCaseBehaviorForFilters() {
        var insensitive = new FindFilter {
            Case = FindCaseSensitivity.None,
            List = { "*.TMP" }
        }.Matchers().Single();
        var sensitive = new FindFilter {
            Case = FindCaseSensitivity.Sensitive,
            List = { "*.TMP" }
        }.Matchers().Single();
        var wrapped = new FindMatcher("needle", ignoreCase: false);

        Assert.That(insensitive.Matches("notes.tmp"), Is.True);
        Assert.That(sensitive.Matches("notes.tmp"), Is.False);
        Assert.That(wrapped.Matches("prefix-needle-suffix"), Is.True);
        Assert.That(wrapped.Matches("prefix-NEEDLE-suffix"), Is.False);
    }

    [Test]
    public void Matches_WildcardIncludesNewlinesInWholeInput() {
        var matcher = new FindMatcher("*", ignoreCase: false);

        Assert.That(matcher.Matches("name\npart"), Is.True);
    }

    private sealed class TestTranslation : ITranslation {
        public string Value(string key) {
            return "Line {0}";
        }

        public string[] Alias(string key) {
            return Array.Empty<string>();
        }
    }
}
