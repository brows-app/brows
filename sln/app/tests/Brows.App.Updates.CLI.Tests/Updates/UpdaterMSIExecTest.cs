using System;
using System.IO;

namespace Brows.Updates;

[TestFixture]
internal sealed class UpdaterMSIExecTest {
    [Test]
    public void StartInfo_UsesSystemInstallerWithPassiveLogging() {
        using (var directory = new HandoffTestDirectory()) {
            var info = UpdaterMSIExec.StartInfo(directory.Msi, directory.Log);
            using (Assert.EnterMultipleScope()) {
                Assert.That(info.FileName, Is.EqualTo(Path.Combine(Environment.SystemDirectory, "msiexec.exe")));
                Assert.That(info.UseShellExecute, Is.False);
                Assert.That(info.ArgumentList,
                    Is.EqualTo(new[] { "/i", directory.Msi, "/passive", "/norestart", "/l*v", directory.Log }));
            }
        }
    }
}
