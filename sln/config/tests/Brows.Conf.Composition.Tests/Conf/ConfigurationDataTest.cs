using System.ComponentModel;

namespace Brows.Conf;

[TestFixture]
internal sealed class ConfigurationDataTest {
    [Test]
    public void Data_SameName_ReturnsSameInstance() {
        IConfigurationData configData = new ConfigurationData();

        var first = configData.Data<TestData>("brows_config_test_same");
        var second = configData.Data<TestData>("brows_config_test_same");

        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public void Data_DifferentName_ReturnsDifferentInstance() {
        IConfigurationData configData = new ConfigurationData();

        var first = configData.Data<TestData>("brows_config_test_one");
        var second = configData.Data<TestData>("brows_config_test_two");

        Assert.That(second, Is.Not.SameAs(first));
    }

    [Test]
    public void Data_NameDifferingInCase_ReturnsSameInstance() {
        IConfigurationData configData = new ConfigurationData();

        var first = configData.Data<TestData>("brows_config_test_case");
        var second = configData.Data<TestData>("BROWS_CONFIG_TEST_CASE");

        /*
         * The name is the name of a file, and file names do not differ
         * in case alone, so both names resolve the same data.
         */
        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public void Data_SameNameDifferentType_Throws() {
        IConfigurationData configData = new ConfigurationData();
        configData.Data<TestData>("brows_config_test_typed");

        /*
         * Data of two types must never share one file.
         */
        Assert.That(
            () => configData.Data<OtherTestData>("brows_config_test_typed"),
            Throws.InvalidOperationException);
    }

    [TestCase((string)null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("brows_config_test|invalid")]
    public void Data_InvalidName_Throws(string name) {
        IConfigurationData configData = new ConfigurationData();

        Assert.That(() => configData.Data<TestData>(name), Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void Data_DifferentInstance_ReturnsSameInstance() {
        var firstData = new ConfigurationData();
        var secondData = new ConfigurationData();

        /*
         * Any instance resolves the same config manager, because the config
         * is shared for the duration of the process no matter how the
         * instance is obtained, such as through the import container or as
         * a fallback in a context without one.
         */
        Assert.That(
            secondData.Data<TestData>("brows_config_test_shared"),
            Is.SameAs(firstData.Data<TestData>("brows_config_test_shared")));
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

    private sealed class OtherTestData : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged {
            add { }
            remove { }
        }
    }
}
