namespace Brows.Updates;

[TestFixture]
internal sealed class UpdaterProgramTest {
    [TestCase("brows-update.exe")]
    [TestCase("\"C:\\path with spaces\\brows-update.exe\"")]
    public void Run_MissingPropertyReturnsTwo(string executable) {
        using (var directory = new HandoffTestDirectory()) {
            var line = string.Join(' ', directory.Properties()[..^1]);
            Assert.That(UpdaterProgram.Run($"{executable} {line}"), Is.EqualTo(2));
        }
    }
}
