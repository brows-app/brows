using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class AppUpdateOutcomeReaderTest {
    [Test]
    public async Task Get_ConsumesOutcomeOnce() {
        using (var directory = new UpdateTestDirectory()) {
            var installation = directory.Installation();
            Directory.CreateDirectory(installation.UpdateDirectory);
            var path = Path.Combine(installation.UpdateDirectory, "outcome.json");
            var expected = new AppUpdateOutcome {
                Version = "0.104.0.61", Kind = AppUpdateOutcomeKind.Failed, ExitCode = 1618,
                Log = Path.Combine(installation.UpdateDirectory, "0.104.0.61", "install.log")
            };
            File.WriteAllText(path, JsonSerializer.Serialize(new {
                version = expected.Version, kind = "Failed", exitCode = expected.ExitCode, log = expected.Log
            }));
            IAppUpdateOutcome reader = new AppUpdateOutcomeReader { Installation = installation };

            using (Assert.EnterMultipleScope()) {
                Assert.That(await reader.Get(CancellationToken.None), Is.EqualTo(expected));
                Assert.That(await reader.Get(CancellationToken.None), Is.Null);
                Assert.That(Directory.GetFiles(installation.UpdateDirectory), Is.Empty);
            }
        }
    }

    [TestCase(null)]
    [TestCase("{")]
    [TestCase("""{"version":"0.104.0.61","kind":"unknown"}""")]
    public async Task Get_MissingOrInvalidOutcomeReturnsNullAndRemovesFile(string content) {
        using (var directory = new UpdateTestDirectory()) {
            var installation = directory.Installation();
            Directory.CreateDirectory(installation.UpdateDirectory);
            if (content is not null) {
                File.WriteAllText(Path.Combine(installation.UpdateDirectory, "outcome.json"), content);
            }
            IAppUpdateOutcome reader = new AppUpdateOutcomeReader { Installation = installation };

            Assert.That(await reader.Get(CancellationToken.None), Is.Null);
            Assert.That(Directory.GetFiles(installation.UpdateDirectory), Is.Empty);
        }
    }
}
