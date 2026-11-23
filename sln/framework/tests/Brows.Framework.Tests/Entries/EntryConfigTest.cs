using System.IO;
using CONF = Domore.Conf.Conf;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryConfigTest {
    [Test]
    public void AutoFit_DefaultsToEnabled() {
        Assert.That(new EntryConfig().AutoFit, Is.True);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AutoFit_ConfiguresInheritedSettingIndependently(bool enabled) {
        var file = Path.GetTempFileName();
        try {
            File.WriteAllText(file, $"AutoFit = {enabled.ToString().ToLowerInvariant()}");
            var configured = new TestConfig();
            var other = new TestConfig();
            CONF.Contain(file, special: "brows").Configure(configured, key: "");
            Assert.That(configured.AutoFit, Is.EqualTo(enabled));
            Assert.That(other.AutoFit, Is.True);
        }
        finally {
            File.Delete(file);
        }
    }

    private sealed class TestConfig : EntryConfig {
    }
}
