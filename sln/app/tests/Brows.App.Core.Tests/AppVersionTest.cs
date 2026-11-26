namespace Brows;

[TestFixture]
internal sealed class AppVersionTest {
    [TestCase("v0.103.0.60", "0.103.0.60", "0.103.0.60", null)]
    [TestCase("0.104.0-beta.61", "0.104.0-beta.61", "0.104.0.61", "beta")]
    [TestCase("0.100.0.0+abc", "0.100.0.0", "0.100.0.0", null)]
    public void TryParse_RecognizesReleaseIdentity(string text, string expectedText, string number, string stage) {
        Assert.That(AppVersion.TryParse(text, out var version), Is.True);
        using (Assert.EnterMultipleScope()) {
            Assert.That(version.Text, Is.EqualTo(expectedText));
            Assert.That(version.Number.ToString(), Is.EqualTo(number));
            Assert.That(version.Stage, Is.EqualTo(stage));
        }
    }

    [TestCase("0.103.0")]
    [TestCase("v1.2")]
    [TestCase("x")]
    [TestCase("")]
    [TestCase(null)]
    [TestCase("0.104.0-a/b.61")]
    [TestCase("0.104.0-..61")]
    [TestCase("0.104.0.2147483648")]
    [TestCase("0.104.0.61\n", TestName = "TryParse_RejectsTrailingNewline")]
    public void TryParse_RejectsInvalidVersion(string text) {
        Assert.That(AppVersion.TryParse(text, out _), Is.False);
    }

    [TestCase("0.103.0.61", true)]
    [TestCase("0.104.0-beta.61", true)]
    [TestCase("0.103.0-rc.60", false)]
    [TestCase("0.103.0.60", false)]
    [TestCase("0.103.0.59", false)]
    public void IsNewerThan_ComparesFourFields(string text, bool newer) {
        Assert.That(AppVersion.TryParse(text, out var candidate), Is.True);
        Assert.That(AppVersion.TryParse("0.103.0.60", out var current), Is.True);
        Assert.That(candidate.IsNewerThan(current), Is.EqualTo(newer));
    }
}
