using System.IO;
using System.Runtime.InteropServices;

namespace Brows.Updates;

[TestFixture]
internal sealed class UpdateInstallationTest {
    [TestCase("APP", "0.103.0.60+abc", Architecture.X64, true)]
    [TestCase("out/Debug", "0.103.0.60", Architecture.X64, false)]
    [TestCase("app", "0.103.0", Architecture.X64, false)]
    [TestCase("app", "0.103.0.60", Architecture.X86, false)]
    public void Installable_RequiresCanonicalDirectoryVersionAndArchitecture(
        string directory, string version, Architecture architecture, bool expected) {
        var root = Path.Combine(Path.GetTempPath(), "Brows");
        var installation = new UpdateInstallation(version, architecture, Architecture.X64,
            Path.Combine(root, directory) + Path.DirectorySeparatorChar, root);
        using (Assert.EnterMultipleScope()) {
            Assert.That(installation.Installable, Is.EqualTo(expected));
            Assert.That(installation.VersionText, Is.EqualTo(version.Split('+')[0]));
            Assert.That(installation.UpdateDirectory, Is.EqualTo(Path.Combine(root, "update")));
        }
    }
}
