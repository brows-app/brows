using Brows.Operations;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryStreamProgressTest {
    private static void VerifyChange(Mock<IOperationProgress> progress,
                                     long? addProgress,
                                     long? setProgress,
                                     long? addTarget,
                                     long? setTarget,
                                     OperationProgressKind? kind) {
        progress.Verify(value => value.Change(
            addProgress: addProgress,
            setProgress: setProgress,
            addTarget: addTarget,
            setTarget: setTarget,
            progressString: null,
            targetString: null,
            name: null,
            data: null,
            kind: kind,
            progressFormat: null,
            targetFormat: null,
            progressRateFormat: null,
            progressRateAverageFormat: null,
            formatProvider: null), Times.Once);
    }

    [Test]
    public async Task Copy_ReportsKnownLengthInBytes() {
        using var source = new MemoryStream([1, 2, 3]);
        using var destination = new MemoryStream();
        var progress = new Mock<IOperationProgress>();

        await progress.Object.Copy(source, destination, CancellationToken.None);

        Assert.That(destination.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 }));
        VerifyChange(
            progress,
            addProgress: null,
            setProgress: 0,
            addTarget: null,
            setTarget: 3,
            kind: OperationProgressKind.FileSize);
        VerifyChange(
            progress,
            addProgress: 3,
            setProgress: null,
            addTarget: null,
            setTarget: null,
            kind: null);
    }

    [Test]
    public async Task Copy_ReadsNonSeekableStreamWithoutLength() {
        using var contents = new MemoryStream([1, 2, 3]);
        var source = new Mock<Stream>();
        source.SetupGet(stream => stream.Length).Throws<NotSupportedException>();
        source.Setup(stream => stream.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>())).Returns((byte[] buffer, int offset, int count, CancellationToken token) =>
                contents.ReadAsync(buffer, offset, count, token));
        using var destination = new MemoryStream();
        var progress = new Mock<IOperationProgress>();

        await progress.Object.Copy(source.Object, destination, CancellationToken.None);

        Assert.That(destination.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 }));
        VerifyChange(
            progress,
            addProgress: null,
            setProgress: 0,
            addTarget: null,
            setTarget: 0,
            kind: OperationProgressKind.FileSize);
        VerifyChange(
            progress,
            addProgress: 3,
            setProgress: null,
            addTarget: 3,
            setTarget: null,
            kind: null);
    }

    [Test]
    public async Task Copy_EmptyStreamReportsFileSizeWithoutByteProgress() {
        using var source = new MemoryStream();
        using var destination = new MemoryStream();
        var progress = new Mock<IOperationProgress>();

        await progress.Object.Copy(source, destination, CancellationToken.None);

        Assert.That(destination.Length, Is.Zero);
        VerifyChange(
            progress,
            addProgress: null,
            setProgress: 0,
            addTarget: null,
            setTarget: 0,
            kind: OperationProgressKind.FileSize);
        progress.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Copy_WriteFailureDoesNotReportUnwrittenBytes() {
        using var source = new MemoryStream([1, 2, 3]);
        var destination = new Mock<Stream>();
        destination.Setup(stream => stream.WriteAsync(
                It.IsAny<byte[]>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException());
        var progress = new Mock<IOperationProgress>();

        await Assert.ThrowsAsync<IOException>(
            () => progress.Object.Copy(source, destination.Object, CancellationToken.None));

        VerifyChange(
            progress,
            addProgress: null,
            setProgress: 0,
            addTarget: null,
            setTarget: 3,
            kind: OperationProgressKind.FileSize);
        progress.VerifyNoOtherCalls();
    }
}
