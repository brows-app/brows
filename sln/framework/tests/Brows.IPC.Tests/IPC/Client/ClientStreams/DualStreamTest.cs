using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IPC.Client.ClientStreams;

[TestFixture]
internal sealed class DualStreamTest {
    [Test]
    public void Constructor_SetsStreamsAndEncoding() {
        using var read = new MemoryStream();
        using var write = new MemoryStream();
        var encoding = Encoding.Unicode;

        using var subject = new DualStream(read, write, encoding);

        using (Assert.EnterMultipleScope()) {
            Assert.That(subject.ReadStream, Is.SameAs(read));
            Assert.That(subject.WriteStream, Is.SameAs(write));
            Assert.That(subject.Encoding, Is.SameAs(encoding));
        }
    }

    [Test]
    public async Task Read_ReturnsLineFromReadStream() {
        using var read = new MemoryStream(Encoding.UTF8.GetBytes("message\n"));
        using var write = new MemoryStream();
        using var subject = new DualStream(read, write);

        var actual = await subject.Read(CancellationToken.None);

        Assert.That(actual, Is.EqualTo("message"));
    }

    [Test]
    public async Task Write_WritesLineToWriteStream() {
        using var read = new MemoryStream();
        using var write = new MemoryStream();
        using var subject = new DualStream(read, write);

        await subject.Write("message", CancellationToken.None);

        var actual = Encoding.UTF8.GetString(write.ToArray());

        Assert.That(actual, Is.EqualTo("message\n"));
    }

    [Test]
    public void Read_CanceledToken_ReturnsCanceledTask() {
        using var read = new MemoryStream();
        using var write = new MemoryStream();
        using var subject = new DualStream(read, write);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAsync<TaskCanceledException>(() => subject.Read(cancellation.Token));
    }

    [Test]
    public void Write_CanceledToken_ReturnsCanceledTask() {
        using var read = new MemoryStream();
        using var write = new MemoryStream();
        using var subject = new DualStream(read, write);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAsync<TaskCanceledException>(() => subject.Write("message", cancellation.Token));
    }
}
