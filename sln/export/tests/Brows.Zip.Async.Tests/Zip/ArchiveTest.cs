using Brows.Collections.Generic;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Brows.Zip;

[TestFixture]
internal sealed class ArchiveTest {
    [Test]
    public void Dispose_PropagatesArchiveCommitFailureAndDisposesRemainingResources() {
        using var bytes = new MemoryStream();
        using (var initial = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true)) {
            var entry = initial.CreateEntry("original.txt");
            using var stream = entry.Open();
            stream.Write(new byte[] { 1, 2, 3 });
        }

        using var backing = new MemoryStream(bytes.ToArray());
        using var streamWithWriteFailure = new CommitFailureStream(backing);
        var zip = new ZipArchive(streamWithWriteFailure, ZipArchiveMode.Update, leaveOpen: true);
        var disposable = new DisposableStack();
        var remaining = new TrackingDisposable();
        disposable.Push(remaining);
        disposable.Push(zip);
        var subject = new Archive(zip, disposable);
        var newEntry = subject.Zip.CreateEntry("new.txt");
        using (var stream = newEntry.Open()) {
            stream.Write(new byte[] { 4, 5, 6 });
        }
        var failure = new IOException("Injected archive commit failure.");
        streamWithWriteFailure.Failure = failure;

        var actual = Assert.Throws<IOException>(subject.Dispose);

        using (Assert.EnterMultipleScope()) {
            Assert.That(actual, Is.SameAs(failure));
            Assert.That(remaining.Disposed, Is.True);
            Assert.That(disposable.Disposed, Is.True);
        }
    }

    private sealed class TrackingDisposable : IDisposable {
        public bool Disposed { get; private set; }

        public void Dispose() {
            Disposed = true;
        }
    }

    private sealed class CommitFailureStream : Stream {
        private readonly Stream Inner;

        public Exception Failure { get; set; }

        public override bool CanRead => Inner.CanRead;
        public override bool CanSeek => Inner.CanSeek;
        public override bool CanWrite => Inner.CanWrite;
        public override long Length => Inner.Length;

        public override long Position {
            get => Inner.Position;
            set => Inner.Position = value;
        }

        public CommitFailureStream(Stream inner) {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public override void Flush() {
            Inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count) {
            return Inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin) {
            return Inner.Seek(offset, origin);
        }

        public override void SetLength(long value) {
            Inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count) {
            ThrowIfRequested();
            Inner.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer) {
            ThrowIfRequested();
            Inner.Write(buffer);
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default) {
            ThrowIfRequested();
            return Inner.WriteAsync(buffer, cancellationToken);
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                Inner.Dispose();
            }
            base.Dispose(disposing);
        }

        private void ThrowIfRequested() {
            if (Failure is not null) {
                throw Failure;
            }
        }
    }
}
