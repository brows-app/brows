using System.Text.RegularExpressions;

namespace Brows.Matching.MatchAlgorithms;

[TestFixture]
internal sealed class RegularExpressionTest {
    [Test]
    public void Matches_CatastrophicPattern_TimesOut() {
        var algorithm = MatchAlgorithm.Create(kind: nameof(RegularExpression));
        var matcher = algorithm.Matcher("(a+)+$");
        var input = new string('a', 40) + "!";
        var error = default(Exception);

        /*
         * A single matching call must finish in bounded time even for a
         * pattern that backtracks catastrophically, where the cancellation
         * check between entries is unreachable.
         */
        var thread = new Thread(() => {
            try {
                matcher.Matches(input);
            }
            catch (Exception e) {
                error = e;
            }
        });
        thread.IsBackground = true;
        thread.Start();

        Assert.That(thread.Join(TimeSpan.FromSeconds(10)), Is.True);
        Assert.That(error, Is.InstanceOf<RegexMatchTimeoutException>());
    }

    [Test]
    public void Matcher_RegularExpression_Matches() {
        var algorithm = MatchAlgorithm.Create(kind: nameof(RegularExpression));
        var matcher = algorithm.Matcher("^he.*o$");

        Assert.That(matcher.Matches("hello"), Is.True);
        Assert.That(matcher.Matches("bye"), Is.False);
    }

    [Test]
    public void Matcher_Wildcard_Matches() {
        var algorithm = MatchAlgorithm.Create(kind: nameof(Wildcard));
        var matcher = algorithm.Matcher("*.cs");

        Assert.That(matcher.Matches("Program.cs"), Is.True);
        Assert.That(matcher.Matches("Program.x"), Is.False);
    }
}
