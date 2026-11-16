using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryStreamConsumerTest {
    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(3)]
    public async Task ConsumeFromMemory_HandlesUnknownLength(long length) {
        using var input = new MemoryStream([1, 2, 3]);
        var source = new Mock<IEntryStreamSource>();
        source.SetupGet(value => value.StreamLength).Returns(length);
        source.SetupGet(value => value.Stream).Returns(input);
        source.Setup(value => value.StreamReady(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntryStreamReady());
        IEntryStreamSet set = new TestSet(source.Object);
        using var output = new MemoryStream();

        await set.ConsumeFromMemory(async (_, stream, _, token) => {
            await stream.CopyToAsync(output, token);
        }, null, CancellationToken.None);

        Assert.That(output.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 }));
    }

    private sealed class TestSet(IEntryStreamSource source) : EntryStreamSet {
        protected override IEnumerable<IEntryStreamSource> StreamSource() {
            return [source];
        }
    }
}
