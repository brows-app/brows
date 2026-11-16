using System.Threading;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryDataTest {
    [Test]
    public void Compare_NullOther_ComparesNonNullGreater() {
        var entryData = new EntryData(Mock.Of<IEntry>(), Mock.Of<IEntryDataDefinition>(), CancellationToken.None);

        /*
         * Null compares lower than any non-null entry data.
         */
        Assert.That(((IEntryData)entryData).Compare(null), Is.EqualTo(1));
    }

    [Test]
    public void Compare_ForeignOther_ComparesEqual() {
        var definition = new Mock<IEntryDataDefinition>();
        var entryData = new EntryData(Mock.Of<IEntry>(), definition.Object, CancellationToken.None);
        var other = new Mock<IEntryData>();

        Assert.That(((IEntryData)entryData).Compare(other.Object), Is.EqualTo(0));
    }

    [Test]
    public void Compare_EntryDataOther_DelegatesToCompareValue() {
        var entry = new Mock<IEntry>();
        var otherEntry = new Mock<IEntry>();
        var definition = new Mock<IEntryDataDefinition>();
        definition.Setup(d => d.CompareValue(entry.Object, otherEntry.Object)).Returns(5);
        var entryData = new EntryData(entry.Object, definition.Object, CancellationToken.None);
        var other = new EntryData(otherEntry.Object, definition.Object, CancellationToken.None);

        Assert.That(((IEntryData)entryData).Compare(other), Is.EqualTo(5));
    }
}
