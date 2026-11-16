using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryComparerTest {
    [Test]
    public void Compare_NullAgainstNonNull_ComparesNullLower() {
        var comparer = new EntryComparer(new Sort());

        Assert.That(comparer.Compare(EntryFor(null), EntryFor(Data())), Is.EqualTo(-1));
    }

    [Test]
    public void Compare_NonNullAgainstNull_ComparesNullLower() {
        var comparer = new EntryComparer(new Sort());

        Assert.That(comparer.Compare(EntryFor(Data()), EntryFor(null)), Is.EqualTo(1));
    }

    [Test]
    public void Compare_NullAgainstNull_ComparesEqual() {
        var comparer = new EntryComparer(new Sort());

        Assert.That(comparer.Compare(EntryFor(null), EntryFor(null)), Is.EqualTo(0));
    }

    [Test]
    public void Compare_MixedSortStaysConsistent() {
        var data = new Mock<IEntryData>();
        data.Setup(d => d.Compare(It.IsAny<IEntryData>())).Returns(0);
        var entry = EntryFor(data.Object);
        var comparer = new EntryComparer(new Sort());

        /*
         * Sorting a list containing null entries must not throw or produce a
         * wrong order.
         */
        var list = new List<IEntry> { entry, null, entry, null };
        Assert.DoesNotThrow(() => list.Sort(comparer));
        Assert.That(list[0], Is.Null);
        Assert.That(list[1], Is.Null);
        Assert.That(list[2], Is.SameAs(entry));
        Assert.That(list[3], Is.SameAs(entry));
    }

    [Test]
    public void Compare_DefinedCompare_PassesThrough() {
        var data = new Mock<IEntryData>();
        var other = new Mock<IEntryData>();
        var entry = EntryFor(data.Object);
        var otherEntry = EntryFor(other.Object);
        data.Setup(d => d.Compare(other.Object)).Returns(5);
        var comparer = new EntryComparer(new Sort());

        Assert.That(comparer.Compare(entry, otherEntry), Is.EqualTo(5));
    }

    private static IEntry EntryFor(IEntryData data) {
        var entry = new Mock<IEntry>();
        entry.Setup(e => e["key"]).Returns(data);
        return entry.Object;
    }

    private static IEntryData Data() {
        var data = new Mock<IEntryData>();
        data.Setup(d => d.Compare(It.IsAny<IEntryData>())).Returns(0);
        return data.Object;
    }

    private sealed class Sort : IEntrySorting {
        private readonly Dictionary<string, EntrySortDirection?> Dictionary = new() { ["key"] = EntrySortDirection.Ascending };

        public EntrySortDirection? this[string key] => key == "key"
            ? Dictionary["key"]
            : throw new KeyNotFoundException(key);

        public int Count =>
            Dictionary.Count;

        public IEnumerable<string> Keys =>
            Dictionary.Keys;

        public IEnumerable<EntrySortDirection?> Values =>
            Dictionary.Values;

        public bool ContainsKey(string key) {
            return Dictionary.ContainsKey(key);
        }

        public bool TryGetValue(string key, out EntrySortDirection? value) {
            return Dictionary.TryGetValue(key, out value);
        }

        public IEntrySorting Change(string key, EntrySortDirection? value) {
            throw new System.NotSupportedException();
        }

        public IEnumerator<KeyValuePair<string, EntrySortDirection?>> GetEnumerator() {
            return Dictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}
