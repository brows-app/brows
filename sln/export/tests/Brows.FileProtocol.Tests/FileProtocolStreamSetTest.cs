using Brows.Tests;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
public sealed class FileProtocolStreamSetTest {
    private TestProvider Provider;

    private static async IAsyncEnumerable<FileProtocolStreamSource<TestEntry>> Sources(params FileProtocolStreamSource<TestEntry>[] sources) {
        foreach (var source in sources) {
            yield return source;
        }
        await Task.CompletedTask;
    }

    [SetUp]
    public void SetUp() {
        Provider = new TestProvider("ftp://host/root/");
    }

    [TearDown]
    public void TearDown() {
        Provider?.Dispose();
        Provider = null;
    }

    [Test]
    public void Constructor_ThrowsWhenTheCollectionIsNull() {
        Assert.Throws<ArgumentNullException>(() => new FileProtocolStreamSet<TestEntry>(null));
    }

    [Test]
    public void StreamSourceConcurrency_DefaultsToOne() {
        IEntryStreamSet subject = new FileProtocolStreamSet<TestEntry>(Sources());
        Assert.That(subject.StreamSourceConcurrency, Is.EqualTo(1));
    }

    [TestCase(3)]
    [TestCase(0)]
    [TestCase(-1)]
    public void StreamSourceConcurrency_IsTheSuppliedValue(int concurrency) {
        IEntryStreamSet subject = new FileProtocolStreamSet<TestEntry>(Sources(), concurrency);
        Assert.That(subject.StreamSourceConcurrency, Is.EqualTo(concurrency));
    }

    [Test]
    public void StreamSourceReady_IsNull() {
        var subject = new FileProtocolStreamSet<TestEntry>(Sources());
        Assert.That(subject.StreamSourceReady(), Is.Null);
    }

    [Test]
    public void FileSource_IsEmpty() {
        var subject = new FileProtocolStreamSet<TestEntry>(Sources());
        Assert.That(subject.FileSource(), Is.Empty);
    }

    [Test]
    public void StreamSource_IsTheCollection() {
        var collection = Sources();
        var subject = new FileProtocolStreamSet<TestEntry>(collection);
        Assert.That(subject.StreamSource(default), Is.SameAs(collection));
    }

    [Test]
    public async Task StreamSource_YieldsTheProvidedSources() {
        var source = new TestStreamSource(new TestEntry(Provider, "ftp://host/root/file.txt"));
        var subject = new FileProtocolStreamSet<TestEntry>(Sources(source));
        var items = new List<IEntryStreamSource>();
        await foreach (var item in subject.StreamSource(default)) {
            items.Add(item);
        }
        Assert.That(items, Is.EqualTo(new[] { source }));
    }
}
