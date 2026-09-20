using NUnit.Framework;
using Brows.Composition;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Config.ConfigPaths;

[TestFixture]
internal sealed class CommonConfigTest {
    [Test]
    public void Root_IsInCommonApplicationData() {
        var subject = new CommonConfig();
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolderOption.DoNotVerify),
            Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]),
            "Config");
        var actual = subject.Root;
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public async Task Root_ChangesWithVariable() {
        var actual = default(string);
        var subject = new CommonConfig();
        var expected = Path.Combine(Path.GetTempPath(), "Brows.Config.Tests", "CommonConfigTest");
        await ImportSandbox.Enter(
            inject: [subject],
            variables: new ImportVariables().Set(new ConfigExportVariable {
                CommonConfig = new() { Path = expected }
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
        var subject = new CommonConfig();
        var expected = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData,
                Environment.SpecialFolderOption.DoNotVerify),
            "relative",
            "path");
        await ImportSandbox.Enter(
            inject: [subject],
            variables: new ImportVariables().Set(new ConfigExportVariable {
                CommonConfig = new() { Path = path }
            }),
            action: () => {
                actual = subject.Root;
            });
        Assert.That(actual, Is.EqualTo(expected));
    }
}
