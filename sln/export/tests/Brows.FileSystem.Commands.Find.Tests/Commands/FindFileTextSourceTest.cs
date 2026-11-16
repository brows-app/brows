using Brows.Support;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Commands;

[TestFixture]
internal sealed class FindFileTextSourceTest {
    [Test]
    public void StreamText_RejectsOversizedOpenFileAndClosesItsHandle() {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("large.txt", "12345");
        var source = new FindFileTextSource(file, 4);

        Assert.Throws<FindContentLimitException>(() => source.StreamText());

        using var exclusive = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.That(exclusive.Length, Is.EqualTo(5));
    }

    [Test]
    public async Task LimitedStream_AllowsExactLimitAndReturnsEndOfFile() {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("exact.txt", "1234");
        var source = new FindFileTextSource(file, 4);
        await using var stream = source.StreamText();
        var bytes = new byte[16];

        Assert.That(await stream.ReadAsync(bytes), Is.EqualTo(4));
        Assert.That(await stream.ReadAsync(bytes), Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LimitedStream_RejectsFileGrowthDuringReading(bool asynchronous) {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("grows.txt", "1234");
        using var reader = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var stream = new FindFileTextSource.LimitedStream(reader, 4);
        var bytes = new byte[4];
        stream.ReadExactly(bytes);
        using (var writer = file.Open(FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) {
            writer.WriteByte(53);
            writer.Flush();
        }

        if (asynchronous) {
            await Assert.ThrowsAsync<FindContentLimitException>(async () => {
                var count = await stream.ReadAsync(bytes);
                Assert.That(count, Is.Zero, "Growth should throw before returning data beyond the limit.");
            });
        }
        else {
            Assert.Throws<FindContentLimitException>(() => {
                var count = stream.Read(bytes);
                Assert.That(count, Is.Zero, "Growth should throw before returning data beyond the limit.");
            });
        }
    }

    [Test]
    public async Task StreamText_AcceptsLargePositiveConfiguredLimitWithoutOverflow() {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("small.txt", "1234");
        var source = new FindFileTextSource(file, long.MaxValue);
        await using var stream = source.StreamText();
        var bytes = new byte[4];

        await stream.ReadExactlyAsync(bytes);

        Assert.That(await stream.ReadAsync(bytes), Is.Zero);
    }
}
