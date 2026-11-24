using System;
using System.IO;

namespace Brows.Conf;

[TestFixture]
internal sealed class ConfigurationPathTest {
    private static string DefaultDataRoot() {
        return Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.DoNotVerify),
            "Brows",
            "data");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    [TestCase(@"relative\data")]
    [TestCase(@"\rooted\without\drive")]
    public void DataRootFrom_ValueIsNotFullyQualified_ReturnsDefault(string value) {
        var dataRoot = ConfigurationPath.DataRootFrom(value);
        Assert.That(dataRoot, Is.EqualTo(DefaultDataRoot()));
    }

    /*
     * Absolute paths are built from the temporary directory, so that they are absolute on every OS.
     */
    private static string Absolute(params string[] parts) {
        return Path.Combine([Path.GetFullPath(Path.GetTempPath()), .. parts]);
    }

    [Test]
    public void DataRootFrom_ValueIsFullyQualified_ReturnsIt() {
        var value = Absolute("agent", "data");
        Assert.That(ConfigurationPath.DataRootFrom(value), Is.EqualTo(value));
    }

    [Test]
    public void DataRootFrom_ValueHasRelativeSegments_ReturnsTheNormalizedPath() {
        var value = Absolute("agent", "..", "session", "data");
        Assert.That(ConfigurationPath.DataRootFrom(value), Is.EqualTo(Absolute("session", "data")));
    }

    [Test]
    public void DataRootFrom_ValueHasSurroundingSpaces_ReturnsTheTrimmedPath() {
        var value = Absolute("agent", "data");
        Assert.That(ConfigurationPath.DataRootFrom($" {value} "), Is.EqualTo(value));
    }

    [Test]
    public void DataRootVariable_IsBrowsDataRoot() {
        Assert.That(ConfigurationPath.DataRootVariable, Is.EqualTo("BROWS_DATA_ROOT"));
    }
}
