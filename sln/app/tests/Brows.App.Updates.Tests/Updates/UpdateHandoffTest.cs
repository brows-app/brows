namespace Brows.Updates;

[TestFixture]
internal sealed class UpdateHandoffTest {
    [Test]
    public void MutexName_MatchesHandoffHelper() {
        Assert.That(UpdateHandoff.MutexName, Is.EqualTo(UpdaterHandoff.MutexName));
    }
}
