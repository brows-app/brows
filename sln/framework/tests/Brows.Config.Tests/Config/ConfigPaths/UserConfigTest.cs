using NUnit.Framework;
using Brows.Composition;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Config.ConfigPaths;

[TestFixture]
internal sealed class UserConfigTest {
    [Test]
    public void Root_IsInLocalApplicationData() {
        var subject = new UserConfig();
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
            Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]),
            "Config");
        var actual = subject.Root;
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public async Task Root_ChangesWithVariable() {
        var actual = default(string);
        var subject = new UserConfig();
        var expected = Path.Combine(Path.GetTempPath(), "Brows.Config.Tests", "UserConfigTest");
        await ImportSandbox.Enter(
            inject: [subject],
            variables: new ImportVariables().Set(new ConfigExportVariable {
                UserConfig = new() { Path = expected }
            }),
            action: () => {
                actual = subject.Root;
            });
        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase("relative/path")]
    [TestCase("relative/path/")]
    [TestCase("relative/path\\")]
    [TestCase("relative\\path")]
    [TestCase("relative\\path")]
    [TestCase("relative\\path\\")]
    [TestCase("relative\\path/")]
    [TestCase("relative\\path/")]
    [TestCase("relative/path/")]
    public async Task Root_HasDefaultPathIfVariableIsRelative(string path) {
        var actual = default(string);
        var subject = new UserConfig();
        var expected = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.DoNotVerify),
            "relative",
            "path");
        await ImportSandbox.Enter(
            inject: [subject],
            variables: new ImportVariables().Set(new ConfigExportVariable {
                UserConfig = new() { Path = path }
            }),
            action: () => {
                actual = subject.Root;
            });
        Assert.That(actual, Is.EqualTo(expected));
    }
}
