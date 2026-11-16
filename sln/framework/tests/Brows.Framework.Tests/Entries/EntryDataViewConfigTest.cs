namespace Brows.Entries;

[TestFixture]
internal sealed class EntryDataViewConfigTest {
    [TestCase(typeof(FileSystemProvider), "entrydataview.filesystem")]
    [TestCase(typeof(ZipPROVIDER), "entrydataview.zip")]
    [TestCase(typeof(Gitprovider), "entrydataview.git")]
    [TestCase(typeof(ProviderOfThings), "entrydataview.providerofthings")]
    [TestCase(typeof(Archive), "entrydataview.archive")]
    public void DataName_ReturnsLowerCaseNameWithoutProviderSuffix(Type providerType, string expected) {
        Assert.That(EntryDataViewConfig.DataName(providerType), Is.EqualTo(expected));
    }

    [Test]
    public void DataName_NullType_Throws() {
        Assert.That(() => EntryDataViewConfig.DataName(null), Throws.ArgumentNullException);
    }

    [TestCase(typeof(Provider))]
    [TestCase(typeof(PROVIDER))]
    public void DataName_TypeNamedProvider_Throws(Type providerType) {
        Assert.That(() => EntryDataViewConfig.DataName(providerType), Throws.ArgumentException);
    }

    private sealed class FileSystemProvider {
    }

    private sealed class ZipPROVIDER {
    }

    private sealed class Gitprovider {
    }

    private sealed class ProviderOfThings {
    }

    private sealed class Archive {
    }

    private sealed class Provider {
    }

    private sealed class PROVIDER {
    }
}
