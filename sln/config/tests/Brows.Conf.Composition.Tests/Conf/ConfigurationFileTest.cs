namespace Brows.Conf;

[TestFixture]
internal sealed class ConfigurationFileTest {
    [Test]
    public void File_SameType_ReturnsSameInstance() {
        IConfigurationFile configFile = new ConfigurationFile();

        var first = configFile.File<TestData>();
        var second = configFile.File<TestData>();

        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public void File_DifferentInstance_ReturnsSameInstance() {
        var firstFile = new ConfigurationFile();
        var secondFile = new ConfigurationFile();

        /*
         * Any instance resolves the same config, because the config is
         * shared for the duration of the process no matter how the instance
         * is obtained, such as through the import container or as a
         * fallback in a context without one.
         */
        Assert.That(secondFile.File<TestData>(), Is.SameAs(firstFile.File<TestData>()));
    }

    private sealed class TestData {
        public string Value { get; set; }
    }
}
