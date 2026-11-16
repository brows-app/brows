using System.Threading;
using System.Threading.Tasks;

namespace Brows.Conf;

[TestFixture]
internal sealed class ConfigurationFileManagerTest {
    [Test]
    public void Loaded_Fresh_ReturnsDefaultWithoutBlocking() {
        var manager = new ConfigurationFileManager<TestConf>();

        /*
         * Loaded must behave like a value-cached result that starts empty:
         * reading it before any successful load returns default and never
         * blocks or starts the load itself.
         */
        Assert.That(manager.Loaded, Is.Null);
    }

    [Test]
    public async Task Load_Twice_ReturnsSameInstance() {
        var manager = new ConfigurationFileManager<TestConf>();

        var first = await manager.Load(CancellationToken.None);
        var second = await manager.Load(CancellationToken.None);

        Assert.That(second, Is.SameAs(first));
        Assert.That(manager.Loaded, Is.SameAs(first));
    }

    private sealed class TestConf {
        public int Value { get; set; }
    }
}