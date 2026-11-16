using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class ZipEntryValidationStreamTest {
    [Test]
    public async Task CopyToAsync_RejectsCrcMismatchAtTheExpectedLength() {
        var contents = Encoding.UTF8.GetBytes("CRC validation payload");
        await using var stream = new ZipEntryValidationStream(
            new MemoryStream(contents, writable: false),
            contents.Length,
            expectedCrc32: uint.MaxValue);
        await Assert.ThrowsAsync<InvalidDataException>(async () => {
            await stream.CopyToAsync(Stream.Null, CancellationToken.None);
        });
    }

    [Test]
    public async Task CopyToAsync_RejectsAnUnexpectedEndOfStream() {
        var contents = Encoding.UTF8.GetBytes("short payload");
        await using var stream = new ZipEntryValidationStream(
            new MemoryStream(contents, writable: false),
            contents.Length + 1,
            expectedCrc32: 0);
        await Assert.ThrowsAsync<InvalidDataException>(async () => {
            await stream.CopyToAsync(Stream.Null, CancellationToken.None);
        });
    }

    [Test]
    public async Task ReadAsync_AllowsAPartialPreviewToCloseWithoutDrainingTheEntry() {
        var contents = Encoding.UTF8.GetBytes("partial preview payload");
        await using var stream = new ZipEntryValidationStream(
            new MemoryStream(contents, writable: false),
            contents.Length,
            expectedCrc32: uint.MaxValue);
        var buffer = new byte[1];
        var read = await stream.ReadAsync(buffer, CancellationToken.None);
        Assert.That(read, Is.EqualTo(1));
    }
}
