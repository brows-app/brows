namespace Brows.Entries;

[TestFixture]
internal sealed class EntryDataConverterTest {
    [TestCase(999, "999 B")]
    [TestCase(1023, "1023 B")]
    [TestCase(1024, "1 KiB")]
    [TestCase(2560, "2.5 KiB")]
    [TestCase(0x100000, "1 MiB")]
    [TestCase(0x40000000, "1 GiB")]
    [TestCase(0x10000000000, "1 TiB")]
    [TestCase(0x4000000000000, "1 PiB")]
    [TestCase(0x5000000000000, "1.25 PiB")]
    [TestCase(0x1000000000000000, "1 EiB")]
    [TestCase(-1024, "-1 KiB")]
    public void FileSize_From_BoundaryValues(long fileLength, string expected) {
        var readable = EntryDataConverter.FileSize.From(fileLength, null, CultureInfo.InvariantCulture);

        Assert.That(readable, Is.EqualTo(expected));
    }
}