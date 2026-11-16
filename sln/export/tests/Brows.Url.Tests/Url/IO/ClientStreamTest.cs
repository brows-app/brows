using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Url.IO;

[TestFixture]
internal sealed class ClientStreamTest {
    private static ClientStream Create(out Action<byte[]> send) {
        ClientDataCallback callback = null;
        var constructor = typeof(ClientStream).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(long), typeof(Action<ClientDataCallback>)], null);
        var stream = (ClientStream)constructor.Invoke([10L, new Action<ClientDataCallback>(value => callback = value)]);
        send = data => callback(data);
        return stream;
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Read_PropagatesFailureAfterBufferedBytes(bool synchronous) {
        using var stream = Create(out var send);
        send([1, 2, 3]);
        var failure = new IOException("Download failed.");
        stream.Complete(failure);
        var buffer = new byte[10];
        var count = synchronous ? stream.Read(buffer, 0, 10) : await stream.ReadAsync(buffer);
        Assert.That(count, Is.EqualTo(3));
        if (synchronous) {
            var error = Assert.Throws<IOException>(() => {
                var unexpected = stream.Read(buffer, 0, 10);
                Assert.Fail($"Expected a transfer error, received {unexpected} bytes.");
            });
            Assert.That(error, Is.SameAs(failure));
        }
        else {
            var error = await Assert.ThrowsAsync<IOException>(async () => {
                var unexpected = await stream.ReadAsync(buffer);
                Assert.Fail($"Expected a transfer error, received {unexpected} bytes.");
            });
            Assert.That(error, Is.SameAs(failure));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Read_PropagatesFailureBeforeOrDuringRead(bool pending) {
        using var stream = Create(out _);
        var failure = new IOException("Download failed.");
        var read = pending ? stream.ReadAsync(new byte[1], 0, 1) : null;
        stream.Complete(failure);
        read ??= stream.ReadAsync(new byte[1], 0, 1);
        var error = await Assert.ThrowsAsync<IOException>(async () => await read.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.That(error, Is.SameAs(failure));
    }

    [Test]
    public async Task Read_ReturnsEofOnlyAfterSuccessfulCompletion() {
        using var stream = Create(out var send);
        send([1, 2, 3]);
        stream.Complete(null);
        using var destination = new MemoryStream();
        await stream.CopyToAsync(destination);
        Assert.That(destination.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 }));
    }

    [Test]
    public async Task Read_CancelingPendingReadDoesNotConsumeData() {
        using var stream = Create(out var send);
        using var cancellation = new CancellationTokenSource();
        var read = stream.ReadAsync(new byte[1], 0, 1, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await read);
        send([7]);
        Assert.That(stream.ReadByte(), Is.EqualTo(7));
    }
}
