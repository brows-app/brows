using NUnit.Framework;
using Brows.Composition;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config.ConfigPaths;

[TestFixture]
internal sealed class DefaultConfigTest {
    private async Task<DefaultConfig> Subject_1(ConfigKind? kind, CancellationToken token) {
        var subject = new DefaultConfig();
        var vary = subject as IExportAndVary<ConfigExportVariable>;
        await vary.Vary(token: token, variable: new() {
            CommonConfig = new() { Path = "foo/bar" },
            UserConfig = new() { Path = "bar/baz" },
            DefaultConfig = kind
        });
        return subject;
    }

    [Test]
    public async Task Kind_DefaultIsUser() {
        var subject = await Subject_1(null, default);
        Assert.That(subject.Kind, Is.EqualTo(ConfigKind.User));
    }

    [Test]
    public async Task Kind_IsUserIfSet() {
        var subject = await Subject_1(ConfigKind.User, default);
        Assert.That(subject.Kind, Is.EqualTo(ConfigKind.User));
    }

    [Test]
    public async Task Root_IsUserRoot() {
        var subject = await Subject_1(ConfigKind.User, default);
        Assert.That(subject.Root, Is.EqualTo(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
                "bar",
                "baz")));
    }

    [Test]
    public async Task Kind_IsCommonIfSet() {
        var subject = await Subject_1(ConfigKind.Common, default);
        Assert.That(subject.Kind, Is.EqualTo(ConfigKind.Common));
    }

    [Test]
    public async Task Root_IsCommonRoot() {
        var subject = await Subject_1(ConfigKind.Common, default);
        Assert.That(subject.Root, Is.EqualTo(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolderOption.DoNotVerify),
                "foo",
                "bar")));
    }
}
