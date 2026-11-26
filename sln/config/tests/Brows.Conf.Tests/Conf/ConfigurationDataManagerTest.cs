using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Conf;

[TestFixture]
internal sealed class ConfigurationDataManagerTest {
    [Test]
    public void Loaded_Fresh_ReturnsDefaultWithoutBlocking() {
        var manager = new ConfigurationDataManager<TestData>("brows_framework_test");

        /*
         * Loaded must behave like a value-cached result that starts empty:
         * reading it before any successful load returns default and never
         * blocks or starts the load itself.
         */
        Assert.That(manager.Loaded, Is.Null);
    }

    [TestCase((string)null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("brows_framework_test|invalid")]
    [TestCase("brows_framework_test/invalid")]
    public void Of_InvalidName_Throws(string name) {
        Assert.That(() => new ConfigurationDataManager<TestData>(name), Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public async Task Save_WritesAndReadsTheNamedFile() {
        var path = Path.Combine(ConfigurationPath.DataRoot, "brows_framework_test_named.brws");
        try {
            var saved = ConfigurationDataPayload.For(new TestData { Value = "value" }, "brows_framework_test_named");
            await saved.Save(CancellationToken.None);

            var loaded = ConfigurationDataPayload.For(new TestData(), "brows_framework_test_named");
            var data = await loaded.Load(CancellationToken.None);

            /*
             * Data is stored under its literal name, so the user can find
             * and edit the file in the data root.
             */
            Assert.That(File.Exists(path), Is.True);
            Assert.That(data.Value, Is.EqualTo("value"));
        }
        finally {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Save_WritesUtf8WithoutByteOrderMark() {
        var path = Path.Combine(ConfigurationPath.DataRoot, "brows_framework_test_encoding.brws");
        try {
            var saved = ConfigurationDataPayload.For(new TestData { Value = "välue" }, "brows_framework_test_encoding");
            await saved.Save(CancellationToken.None);

            var bytes = await File.ReadAllBytesAsync(path);
            var startsWithByteOrderMark = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

            Assert.That(startsWithByteOrderMark, Is.False);
            Assert.That(Encoding.UTF8.GetString(bytes), Does.Contain("välue"));
        }
        finally {
            File.Delete(path);
        }
    }

    [TestCase("@brws")]
    [TestCase("@conf")]
    public async Task Load_IgnoresIncludeDirectives(string special) {
        var path = Path.Combine(ConfigurationPath.DataRoot, "brows_framework_test_directive.brws");
        var included = Path.Combine(ConfigurationPath.DataRoot, "brows_framework_test_directive_included.brws");
        try {
            await File.WriteAllTextAsync(included, "Value = included\r\n");
            await File.WriteAllTextAsync(path, $"{special}.include = {included}\r\n");

            var loaded = ConfigurationDataPayload.For(new TestData(), "brows_framework_test_directive");
            var data = await loaded.Load(CancellationToken.None);

            /*
             * Data files do not support special directives, so an include
             * line is an unmatched key rather than a reference to another file.
             */
            Assert.That(data.Value, Is.Null);
        }
        finally {
            File.Delete(path);
            File.Delete(included);
        }
    }

    [Test]
    public async Task Load_Twice_ReturnsSameInstance() {
        var manager = new ConfigurationDataManager<TestData>("brows_framework_test");

        var first = await manager.Load(CancellationToken.None);
        var second = await manager.Load(CancellationToken.None);

        Assert.That(second, Is.SameAs(first));
        Assert.That(manager.Loaded, Is.SameAs(first));
    }

    [Test]
    public async Task Load_Concurrent_ReturnsSameInstance() {
        var manager = new ConfigurationDataManager<TestData>("brows_framework_test");
        TestData resultA = null;
        TestData resultB = null;

        var entered = new ManualResetEventSlim(false);
        var release = new ManualResetEventSlim(false);

        var threadA = new Thread(() => {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(5), CancellationToken.None);
            resultA = manager.Load(CancellationToken.None).GetAwaiter().GetResult();
        });
        var threadB = new Thread(() => {
            entered.Wait(TimeSpan.FromSeconds(5), CancellationToken.None);
            release.Set();
            resultB = manager.Load(CancellationToken.None).GetAwaiter().GetResult();
        });
        threadA.Start();
        threadB.Start();

        Assert.That(threadA.Join(TimeSpan.FromSeconds(30)), Is.True);
        Assert.That(threadB.Join(TimeSpan.FromSeconds(30)), Is.True);

        /*
         * Concurrent first loads must share one cache: both callers observe
         * the same instance rather than separate caches created by a race.
         */
        Assert.That(resultA, Is.Not.Null);
        Assert.That(resultB, Is.SameAs(resultA));
        Assert.That(manager.Loaded, Is.SameAs(resultA));
    }

    private sealed class TestData : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        public string Value {
            get => _Value;
            set {
                _Value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
        private string _Value;
    }
}