using Brows.Tests;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
public sealed class FileProtocolPreviewStreamSourceTest {
    private TestProvider Provider;

    [SetUp]
    public void SetUp() {
        Provider = new TestProvider("ftp://host/root/");
    }

    [TearDown]
    public void TearDown() {
        Provider?.Dispose();
        Provider = null;
    }

    private FileProtocolPreviewStreamSource<TestEntry> Source(TestEntry entry,
                                                              Stream stream,
                                                              long? readLimit,
                                                              Action<TestEntry, CancellationToken> opened = null) {
        return new FileProtocolPreviewStreamSource<TestEntry>(
            entry,
            entry.Name,
            streamLength: stream.CanSeek ? stream.Length : 0,
            readLimit,
            (selected, token) => {
                opened?.Invoke(selected, token);
                token.ThrowIfCancellationRequested();
                return Task.FromResult(stream);
            });
    }

    [Test]
    public async Task Source_UsesTheSelectedEntryAndKeepsTheRemoteStreamReadOnly() {
        var first = new TestEntry(Provider, "ftp://host/root/first.txt") { NameValue = "first.txt" };
        var second = new TestEntry(Provider, "ftp://host/root/second.txt") { NameValue = "second.txt" };
        var selectedStream = new NonSeekableStream("second preview");
        TestEntry openedEntry = null;
        var source = Source(second, selectedStream, readLimit: 1024, (entry, _) => openedEntry = entry);

        using var ready = await ((IEntryStreamSource)source).StreamReady(CancellationToken.None);
        var remote = ((IEntryStreamSource)source).Stream;
        using var text = new MemoryStream();
        await remote.CopyToAsync(text);

        using (Assert.EnterMultipleScope()) {
            Assert.That(((IEntryStreamSource)source).EntryID, Is.EqualTo(second.ID));
            Assert.That(openedEntry, Is.SameAs(second));
            Assert.That(((IEntryStreamSource)source).RelativePath, Is.EqualTo("second.txt"));
            Assert.That(((IEntryStreamSource)source).StreamLength, Is.EqualTo(0));
            Assert.That(remote.CanRead, Is.True);
            Assert.That(remote.CanSeek, Is.False);
            Assert.That(remote.CanWrite, Is.False);
            Assert.That(Encoding.UTF8.GetString(text.ToArray()), Is.EqualTo("second preview"));
            Assert.That(first.ID, Is.Not.EqualTo(((IEntryStreamSource)source).EntryID));
        }

        await remote.DisposeAsync();
        Assert.That(selectedStream.Disposed, Is.True);
    }

    [Test]
    public void Constructor_RejectsNegativePreviewReadLimits() {
        var entry = new TestEntry(Provider, "ftp://host/root/negative.txt");

        Assert.Throws<ArgumentOutOfRangeException>(() => new FileProtocolPreviewStreamSource<TestEntry>(
            entry,
            entry.Name,
            streamLength: 0,
            readLimit: -1,
            (_, _) => Task.FromResult<Stream>(new NonSeekableStream("unused"))));
    }

    [Test]
    public async Task Read_ValidatesArrayBufferArguments() {
        var entry = new TestEntry(Provider, "ftp://host/root/file.txt");
        var source = Source(entry, new NonSeekableStream("data"), readLimit: 1024);
        using var ready = await ((IEntryStreamSource)source).StreamReady(CancellationToken.None);
        var remote = ((IEntryStreamSource)source).Stream;
        Func<byte[], int, int, CancellationToken, Task<int>> read = remote.ReadAsync;

        await Assert.ThrowsAsync<ArgumentNullException>(async () => {
            await read(null, 0, 0, CancellationToken.None);
        });
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => {
            await read(new byte[1], 0, -1, CancellationToken.None);
        });
        await remote.DisposeAsync();
    }

    [Test]
    public async Task StreamReady_PropagatesCancellationToTheBackendOpen() {
        var entry = new TestEntry(Provider, "ftp://host/root/cancel.txt");
        var stream = new NonSeekableStream("unused");
        CancellationToken backendToken = default;
        var source = Source(entry, stream, readLimit: 1024, (_, token) => backendToken = token);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => {
            await ((IEntryStreamSource)source).StreamReady(cancellation.Token);
        });
        Assert.That(backendToken.IsCancellationRequested, Is.True);
        Assert.That(stream.Disposed, Is.False);
    }

    [Test]
    public async Task ReadAsync_StopsAtTheConfiguredLimitAndCancelsTheRemoteTransfer() {
        var entry = new TestEntry(Provider, "ftp://host/root/large.txt");
        var stream = new NonSeekableStream("0123456789");
        CancellationToken backendToken = default;
        var source = Source(entry, stream, readLimit: 4, (_, token) => backendToken = token);

        using var ready = await ((IEntryStreamSource)source).StreamReady(CancellationToken.None);
        var remote = ((IEntryStreamSource)source).Stream;
        Func<Memory<byte>, CancellationToken, ValueTask<int>> read = remote.ReadAsync;
        var preview = new byte[4];
        var previewLength = await read(preview.AsMemory(), CancellationToken.None);
        var repeatedEof = await read(new byte[1].AsMemory(), CancellationToken.None);
        var secondRepeatedEof = await read(new byte[1].AsMemory(), CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(Encoding.UTF8.GetString(preview, 0, previewLength), Is.EqualTo("0123"));
            Assert.That(backendToken.IsCancellationRequested, Is.True);
            Assert.That(repeatedEof, Is.EqualTo(0));
            Assert.That(secondRepeatedEof, Is.EqualTo(0));
        }

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.CatchAsync<OperationCanceledException>(async () => {
            await read(new byte[1].AsMemory(), cancellation.Token);
        });

        await remote.DisposeAsync();
        Assert.That(stream.Disposed, Is.True);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => {
            await read(new byte[1].AsMemory(), CancellationToken.None);
        });
    }

    [TestCase(3, 3, false, TestName = "ReadAsync_PreviewProbeBelowLimit_ReachesRemoteEof")]
    [TestCase(4, 4, false, TestName = "ReadAsync_PreviewProbeAtLimit_ReachesRemoteEof")]
    [TestCase(6, 5, true, TestName = "ReadAsync_PreviewProbeOverLimit_StopsAfterOneExtraByte")]
    public async Task ReadAsync_PreviewProbe_ReservesOneByteForOverflow(int length, int expected, bool canceled) {
        const int markdownLimit = 4;
        var entry = new TestEntry(Provider, "ftp://host/root/unknown-size.md");
        var content = new string('x', length);
        CancellationToken backendToken = default;
        IEntryStreamSource source = Source(entry, new NonSeekableStream(content), readLimit: markdownLimit + 1,
            (_, token) => backendToken = token);

        using var ready = await source.StreamReady(CancellationToken.None);
        await using var remote = source.Stream;
        using var preview = new MemoryStream();
        await remote.CopyToAsync(preview);

        using (Assert.EnterMultipleScope()) {
            Assert.That(Encoding.UTF8.GetString(preview.ToArray()), Is.EqualTo(content[..expected]));
            Assert.That(backendToken.IsCancellationRequested, Is.EqualTo(canceled));
        }
    }

    [Test]
    public async Task ReadAsync_ZeroLimitReturnsStableEofAndCancelsTheRemoteTransfer() {
        var entry = new TestEntry(Provider, "ftp://host/root/empty-limit.txt");
        var stream = new NonSeekableStream("unused", failRead: true);
        CancellationToken backendToken = default;
        var source = Source(entry, stream, readLimit: 0, (_, token) => backendToken = token);

        using var ready = await ((IEntryStreamSource)source).StreamReady(CancellationToken.None);
        var remote = ((IEntryStreamSource)source).Stream;
        Func<Memory<byte>, CancellationToken, ValueTask<int>> read = remote.ReadAsync;
        var firstEof = await read(new byte[1].AsMemory(), CancellationToken.None);
        Assert.That(backendToken.IsCancellationRequested, Is.True);
        var secondEof = await read(new byte[1].AsMemory(), CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(firstEof, Is.EqualTo(0));
            Assert.That(secondEof, Is.EqualTo(0));
            Assert.That(backendToken.IsCancellationRequested, Is.True);
            Assert.That(stream.Disposed, Is.False);
        }

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.CatchAsync<OperationCanceledException>(async () => {
            await read(new byte[1].AsMemory(), cancellation.Token);
        });

        await remote.DisposeAsync();
        Assert.That(stream.Disposed, Is.True);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => {
            await read(new byte[1].AsMemory(), CancellationToken.None);
        });
    }

    [Test]
    public async Task ReadAsync_CanBeCancelledAfterTheRemoteReadBegins() {
        var entry = new TestEntry(Provider, "ftp://host/root/wait.txt");
        var stream = new BlockingStream();
        var source = new FileProtocolPreviewStreamSource<TestEntry>(
            entry,
            entry.Name,
            streamLength: 0,
            readLimit: 1024,
            (_, _) => Task.FromResult<Stream>(stream));
        using var ready = await ((IEntryStreamSource)source).StreamReady(CancellationToken.None);
        var remote = ((IEntryStreamSource)source).Stream;
        using var cancellation = new CancellationTokenSource();
        var read = remote.ReadAsync(new byte[1].AsMemory(), cancellation.Token).AsTask();
        await stream.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.CatchAsync<OperationCanceledException>(async () => await read);
        await remote.DisposeAsync();

        Assert.That(stream.Disposed, Is.True);
    }

    [Test]
    public async Task ReadFailure_PropagatesAndDisposesTheRemoteStream() {
        var entry = new TestEntry(Provider, "ftp://host/root/broken.txt");
        var stream = new NonSeekableStream("unused", failRead: true);
        var source = Source(entry, stream, readLimit: 1024);
        using var ready = await ((IEntryStreamSource)source).StreamReady(CancellationToken.None);
        var remote = ((IEntryStreamSource)source).Stream;

        await Assert.ThrowsAsync<IOException>(async () => {
            await remote.CopyToAsync(Stream.Null);
        });
        await remote.DisposeAsync();

        Assert.That(stream.Disposed, Is.True);
    }

    private sealed class NonSeekableStream : Stream {
        private readonly byte[] Data;
        private readonly bool FailRead;
        private int ReadPosition;

        public bool Disposed { get; private set; }

        public sealed override bool CanRead => !Disposed;
        public sealed override bool CanSeek => false;
        public sealed override bool CanWrite => false;
        public sealed override long Length => throw new NotSupportedException();
        public sealed override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public NonSeekableStream(string data, bool failRead = false) {
            Data = Encoding.UTF8.GetBytes(data);
            FailRead = failRead;
        }

        protected sealed override void Dispose(bool disposing) {
            if (disposing) {
                Disposed = true;
            }
            base.Dispose(disposing);
        }

        public sealed override ValueTask DisposeAsync() {
            Disposed = true;
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }

        public sealed override void Flush() {
            throw new NotSupportedException();
        }

        public sealed override int Read(byte[] buffer, int offset, int count) {
            return ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        }

        public sealed override async ValueTask<int> ReadAsync(Memory<byte> buffer,
                                                              CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            if (FailRead) {
                throw new IOException("The remote read failed.");
            }
            var count = Math.Min(buffer.Length, Data.Length - ReadPosition);
            Data.AsMemory(ReadPosition, count).CopyTo(buffer);
            ReadPosition += count;
            await Task.CompletedTask;
            return count;
        }

        public sealed override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public sealed override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public sealed override void Write(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }
    }

    private sealed class BlockingStream : Stream {
        public TaskCompletionSource ReadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Disposed { get; private set; }

        public sealed override bool CanRead => !Disposed;
        public sealed override bool CanSeek => false;
        public sealed override bool CanWrite => false;
        public sealed override long Length => throw new NotSupportedException();
        public sealed override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        protected sealed override void Dispose(bool disposing) {
            if (disposing) {
                Disposed = true;
            }
            base.Dispose(disposing);
        }

        public sealed override ValueTask DisposeAsync() {
            Disposed = true;
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }

        public sealed override void Flush() {
            throw new NotSupportedException();
        }

        public sealed override int Read(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }

        public sealed override async ValueTask<int> ReadAsync(Memory<byte> buffer,
                                                              CancellationToken cancellationToken = default) {
            ReadEntered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public sealed override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public sealed override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public sealed override void Write(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }
    }
}
