namespace Brows.Updates;

[TestFixture]
internal sealed class UpdaterArgumentsTest {
    [TestCase("pid=123")]
    [TestCase("PID=0x7b")]
    public void TryParse_BindsPropertiesIncludingPathsWithSpacesAndPunctuation(string pid) {
        using (var directory = new HandoffTestDirectory()) {
            var properties = directory.Properties();
            properties[0] = pid;
            Assert.That(UpdaterArguments.TryParse(string.Join(' ', properties), out var args), Is.True);
            using (Assert.EnterMultipleScope()) {
                Assert.That(args.Pid, Is.EqualTo(123));
                Assert.That(args.Msi, Is.EqualTo(directory.Msi));
                Assert.That(args.Log, Is.EqualTo(directory.Log));
                Assert.That(args.Outcome, Is.EqualTo(directory.Outcome));
                Assert.That(args.Version.Text, Is.EqualTo(directory.Version));
                Assert.That(args.Updates, Is.EqualTo(directory.Updates));
                Assert.That(args.Launch, Is.EqualTo(directory.Launch));
            }
        }
    }

    [TestCase(0, "pid=not-a-pid")]
    [TestCase(0, "pid=0")]
    [TestCase(1, "msi=relative.msi")]
    [TestCase(4, "version=v0.103.0.62")]
    [TestCase(6, "unknown=value")]
    public void TryParse_InvalidPropertyReturnsFalse(int index, string replacement) {
        using (var directory = new HandoffTestDirectory()) {
            var properties = directory.Properties();
            properties[index] = replacement;
            Assert.That(UpdaterArguments.TryParse(string.Join(' ', properties), out _), Is.False);
        }
    }

    [Test]
    public void TryParse_RejectsSwitchValueSyntax() {
        using (var directory = new HandoffTestDirectory()) {
            var line = "--pid 123 " + string.Join(' ', directory.Properties()[1..]);
            Assert.That(UpdaterArguments.TryParse(line, out _), Is.False);
        }
    }
}
