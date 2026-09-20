using NUnit.Framework;
using Brows.Composition;
using Brows.Config.ConfigPaths;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config;

[TestFixture]
public sealed class ConfigPathTest {
    private string TempPath;
    private string UserRoot;
    private string CommonRoot;

    private Task Enter(Func<Task> test) {
        return ImportSandbox.Enter(
            function: test,
            inject: [new UserConfig(), new CommonConfig()],
            variables: new ImportVariables().Set(new ConfigExportVariable {
                CommonConfig = new() { Path = CommonRoot },
                UserConfig = new() { Path = UserRoot }
            }));
    }

    private class CycleConf {
        public int NumberOfWheels { get; set; }
    }

    [SetUp]
    public void SetUp() {
        TempPath = Path.Combine(Path.GetTempPath(), $"{GetType().FullName}_{Guid.NewGuid()}");
        UserRoot = Path.Combine(TempPath, "User");
        CommonRoot = Path.Combine(TempPath, "Common");
    }

    [TearDown]
    public async Task TearDown() {
        await Task.Run(() => {
            if (Directory.Exists(TempPath)) {
                Directory.Delete(TempPath, recursive: true);
            }
        });
    }

    [TestCase(1)]
    [TestCase(2)]
    public Task CanConfigureUserConfObject(int n) => Enter(async () => {
        var config = Imports.Current.Find<IConfigUser>();
        await config.Write(new CycleConf { NumberOfWheels = n }, default);
        var actual = await config.Read<CycleConf>(default);
        Assert.That(actual.NumberOfWheels, Is.EqualTo(n));
    });


    [Test]
    public Task CanUpdateUserConfObject() => Enter(async () => {
        var config = Imports.Current.Find<IConfigUser>();
        await config.Write(new CycleConf { NumberOfWheels = 3 }, default);
        await config.Update<CycleConf>(m => m.NumberOfWheels = 1, default);
        var actual = await config.Read<CycleConf>(default);
        Assert.That(actual.NumberOfWheels, Is.EqualTo(1));
    });

    [Test]
    public Task CanWatchUserConfObject() => Enter(async () => {
        var config = Imports.Current.Find<IConfigUser>();
        await config.Write(new CycleConf { NumberOfWheels = 6 }, default);
        var configured = 0;
        var obj = await config.Watch<CycleConf>(
            token: default,
            errored: null,
            configured: () => {
                configured++;
            });
        await config.Update<CycleConf>(c => c.NumberOfWheels = 7, default);
        await Task.Run(() => {
            SpinWait.SpinUntil(() => configured > 0, TimeSpan.FromSeconds(5));
        });
        await Task.Run(() => {
            SpinWait.SpinUntil(() => obj.NumberOfWheels == 7, TimeSpan.FromSeconds(1));
        });
        using (Assert.EnterMultipleScope()) {
            Assert.That(configured, Is.EqualTo(1));
            Assert.That(obj.NumberOfWheels, Is.EqualTo(7));
        }
    });

    [TestCase(3)]
    [TestCase(2)]
    public Task CanConfigureCommonConfObject(int n) => Enter(async () => {
        var config = Imports.Current.Find<IConfigCommon>();
        await config.Write(new CycleConf { NumberOfWheels = n }, default);
        var actual = await config.Read<CycleConf>(default);
        Assert.That(actual.NumberOfWheels, Is.EqualTo(n));
    });

    [Test]
    public Task CanUpdateCommonConfObject() => Enter(async () => {
        var config = Imports.Current.Find<IConfigUser>();
        await config.Write(new CycleConf { NumberOfWheels = 2 }, default);
        await config.Update<CycleConf>(m => m.NumberOfWheels = 3, default);
        var actual = await config.Read<CycleConf>(default);
        Assert.That(actual.NumberOfWheels, Is.EqualTo(3));
    });

    class PollingInfo {
        public TimeSpan Rate { get; set; } = TimeSpan.FromSeconds(1.234);
    }

    [Test]
    public Task TypeWithAmbiguousNameCanBeRead() => Enter(async () => {
        var config = Imports.Current.Find<IConfigUser>();
        var model = await config.Read<PollingInfo>(default);
        Assert.That(model.Rate, Is.EqualTo(TimeSpan.FromSeconds(1.234)));
    });

    [TestCase(2.25)]
    [TestCase(3.50)]
    public Task TypeWithAmbiguousNameCanBeWritten(double rate) => Enter(async () => {
        var config = Imports.Current.Find<IConfigUser>();
        await config.Write(new PollingInfo { Rate = TimeSpan.FromSeconds(rate) }, default);
        var model = await config.Read<PollingInfo>(default);
        Assert.That(model.Rate.TotalSeconds, Is.EqualTo(rate));
    });

    class SomeConfig {
        public string SomeVal { get; set; } = "My value";
    }

    [Test]
    public Task TypeWithNameThatEndsWithConfigCanBeRead() => Enter(async () => {
        var config = Imports.Current.Find<IConfigUser>();
        var model = await config.Read<SomeConfig>(default);
        Assert.That(model.SomeVal, Is.EqualTo("My value"));
    });

    [TestCase("hello, world")]
    [TestCase("Goodbye, Earth")]
    public Task TypeWithNameThatEndsWithConfigCanBeWritten(string someVal) => Enter(async () => {
        var config = Imports.Current.Find<IConfigUser>();
        await config.Write(new SomeConfig { SomeVal = someVal }, default);
        var model = await config.Read<SomeConfig>(default);
        Assert.That(model.SomeVal, Is.EqualTo(someVal));
    });

    private static async Task PathExists<TConfig, TObject>(string expectedPath, string expectedContent)
    where TConfig : IConfig, IExport
    where TObject : new() {
        await Task.Run(() => {
            try {
                File.Delete(expectedPath);
            }
            catch (IOException) {
                // File may not exist.
            }
        });
        var subject = Imports.Current.Find<TConfig>();
        var model = await subject.Read<TObject>(default);
        var pathContent = await Task.Run(() => File.ReadAllText(expectedPath));
        Assert.That(pathContent, Is.EqualTo(expectedContent));
    }

    [Test]
    public Task TypeWithNameThatEndsWithConfigExistsInUserJsonPath() => Enter(async () => {
        await PathExists<IConfigUser, SomeConfig>(
            expectedPath: Path.Combine(UserRoot, "Json", "Some.json"),
            expectedContent: "{\r\n  \"SomeVal\": \"My value\"\r\n}");
    });

    [Test]
    public Task TypeWithNameThatEndsWithConfigExistsInCommonJsonPath() => Enter(async () => {
        await PathExists<IConfigCommon, SomeConfig>(
            expectedPath: Path.Combine(CommonRoot, "Json", "Some.json"),
            expectedContent: "{\r\n  \"SomeVal\": \"My value\"\r\n}");
    });

    class SomeJson : SomeConfig {
    }

    [Test]
    public Task TypeWithNameThatEndsWithJsonExistsInUserJsonPath() => Enter(async () => {
        await PathExists<IConfigUser, SomeJson>(
            expectedPath: Path.Combine(UserRoot, "Json", "Some.json"),
            expectedContent: "{\r\n  \"SomeVal\": \"My value\"\r\n}");
    });

    [Test]
    public Task TypeWithNameThatEndsWithJsonExistsInCommonJsonPath() => Enter(async () => {
        await PathExists<IConfigCommon, SomeJson>(
            expectedPath: Path.Combine(CommonRoot, "Json", "Some.json"),
            expectedContent: "{\r\n  \"SomeVal\": \"My value\"\r\n}");
    });

    class SomeConf : SomeConfig {
    }

    [Test]
    public Task TypeWithNameThatEndsWithConfExistsInUserConfPath() => Enter(async () => {
        await PathExists<IConfigUser, SomeConf>(
            expectedPath: Path.Combine(UserRoot, "Conf", "Some.conf"),
            expectedContent: "SomeVal = My value");
    });

    [Test]
    public Task TypeWithNameThatEndsWithConfExistsInCommonConfPath() => Enter(async () => {
        await PathExists<IConfigCommon, SomeConf>(
            expectedPath: Path.Combine(CommonRoot, "Conf", "Some.conf"),
            expectedContent: "SomeVal = My value");
    });

    class SomeData : SomeConfig {
    }

    [Test]
    public Task TypeWithNameThatEndsWithDataExistsInUserDataPath() => Enter(async () => {
        await PathExists<IConfigUser, SomeData>(
            expectedPath: Path.Combine(UserRoot, "Data", "Some.json"),
            expectedContent: "{\r\n  \"SomeVal\": \"My value\"\r\n}");
    });

    [Test]
    public Task TypeWithNameThatEndsWithDataExistsInCommonDataPath() => Enter(async () => {
        await PathExists<IConfigCommon, SomeData>(
            expectedPath: Path.Combine(CommonRoot, "Data", "Some.json"),
            expectedContent: "{\r\n  \"SomeVal\": \"My value\"\r\n}");
    });
}
