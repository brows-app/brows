using Domore.Conf;
using NUnit.Framework;
using Brows.Composition;
using Brows.Config.ConfigPaths;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config;

[TestFixture]
internal sealed class ConfigObjectProviderTest {
    private string TempPath;
    private string UserRoot;
    private string CommonRoot;

    private Task Enter(Func<Task> test) {
        return ImportSandbox.Enter(
            function: test,
            inject: [new UserConfig(), new CommonConfig(), new DefaultConfig(), new ConfigObjectProvider()],
            variables: new ImportVariables().Set(new ConfigExportVariable {
                CommonConfig = new() { Path = CommonRoot },
                UserConfig = new() { Path = UserRoot },
                DefaultConfig = ConfigKind.User
            }));
    }

    private class CycleConf {
        public int NumberOfWheels { get; set; }
    }

    [SetUp]
    public void SetUp() {
        TempPath = Path.Combine(Path.GetTempPath(), "Brows.Config.Tests", "ConfigObjectProviderTest", Guid.NewGuid().ToString());
        UserRoot = Path.Combine(TempPath, "User");
        CommonRoot = Path.Combine(TempPath, "Common");
    }

    [TearDown]
    public async Task TearDown() {
        await Task.Run(() => Directory.Delete(TempPath, recursive: true));
    }

    [Test]
    public Task Get_ReturnsDefaultConfigurationObject() => Enter(async () => {
        var provider = Imports.Current.Find<IConfigObjectProvider>();
        var obj = await provider.Get<CycleConf>(default);
        Assert.That(obj.NumberOfWheels, Is.Zero);
    });

    [Test]
    public Task Get_WatchesForChanges() => Enter(async () => {
        var provider = Imports.Current.Find<IConfigObjectProvider>();
        var obj = await provider.Get<CycleConf>(default);
        var config = Imports.Current.Find<IConfigDefault>();
        await config.Update<CycleConf>(c => c.NumberOfWheels = 101, default);
        var timeout = !SpinWait.SpinUntil(() => obj.NumberOfWheels > 0, timeout: TimeSpan.FromSeconds(5));
        using (Assert.EnterMultipleScope()) {
            Assert.That(timeout, Is.False);
            Assert.That(obj.NumberOfWheels, Is.EqualTo(101));
        }
    });

    [Test]
    public Task Get_GetsCachedInstance() => Enter(async () => {
        var provider = Imports.Current.Find<IConfigObjectProvider>();
        var obj1 = await provider.Get<CycleConf>(default);
        var obj2 = await provider.Get<CycleConf>(default);
        Assert.That(obj1, Is.SameAs(obj2));
    });

    [Test]
    public Task Get_GetsCachedInstanceRegardlessOfThread() => Enter(async () => {
        var provider = Imports.Current.Find<IConfigObjectProvider>();
        var tasks = Enumerable
            .Range(0, 100)
            .Select(_ => Task.Run(() => provider.Get<CycleConf>(default)));
        var items = await Task.WhenAll(tasks);
        Assert.That(items, Has.All.SameAs(items[0]));
    });
}
