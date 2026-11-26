using Brows.Operations;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Updates;

[TestFixture]
internal sealed class UpdateDownloadTest {
    [Test]
    public async Task File_WritesVerifiedBytesAndReportsProgress() {
        using (var directory = new UpdateTestDirectory())
        using (var http = new HttpClient(new UpdateHttpHandler(_ =>
                   new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(directory.Bytes) }))) {
            var update = directory.Update();
            var progress = new Mock<IOperationProgress>();
            var path = await new UpdateDownload(http).File(update, directory.Root, progress.Object,
                CancellationToken.None);

            using (Assert.EnterMultipleScope()) {
                Assert.That(path, Is.EqualTo(Path.Combine(directory.Root, update.Package.Name)));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(directory.Bytes));
                Assert.That(Directory.GetFiles(directory.Root), Is.EqualTo(new[] { path }));
            }
            Assert.That(progress.Invocations.Any(call => Equals(call.Arguments[3], update.Package.Size)
                && Equals(call.Arguments[8], OperationProgressKind.FileSize)), Is.True, "expected target and unit");
            Assert.That(progress.Invocations.Any(call => Equals(call.Arguments[0], update.Package.Size)), Is.True,
                "expected downloaded bytes");
        }
    }

    [TestCase("http", typeof(HttpRequestException))]
    [TestCase("length header", typeof(InvalidDataException))]
    [TestCase("oversized", typeof(InvalidDataException))]
    [TestCase("truncated", typeof(InvalidDataException))]
    [TestCase("wrong hash", typeof(InvalidDataException))]
    [TestCase("read error", typeof(IOException))]
    [TestCase("stall", typeof(TimeoutException))]
    public void File_FailureLeavesNoInstallerOrPartial(string failure, Type error) {
        using (var directory = new UpdateTestDirectory())
        using (var http = new HttpClient(new UpdateHttpHandler(_ => Response(failure, directory.Bytes)))) {
            var download = new UpdateDownload(http) { IdleTimeout = TimeSpan.FromMilliseconds(100) };
            Assert.That(async () => await download.File(directory.Update(), directory.Root,
                    Mock.Of<IOperationProgress>(), CancellationToken.None),
                Throws.InstanceOf(error));
            Assert.That(Directory.GetFiles(directory.Root), Is.Empty);
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task File_ReusesOnlyVerifiedCache(bool verified) {
        using (var directory = new UpdateTestDirectory())
        using (var handler = new UpdateHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)))
        using (var http = new HttpClient(handler)) {
            var update = directory.Update();
            var path = Path.Combine(directory.Root, update.Package.Name);
            File.WriteAllBytes(path, verified ? directory.Bytes : [4, 3, 2, 1]);
            var download = new UpdateDownload(http);

            if (verified) {
                var result = await download.File(update, directory.Root, Mock.Of<IOperationProgress>(),
                    CancellationToken.None);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(result, Is.EqualTo(path));
                    Assert.That(handler.Requests, Is.Empty);
                }
                return;
            }

            Assert.That(async () => await download.File(update, directory.Root, Mock.Of<IOperationProgress>(),
                CancellationToken.None), Throws.TypeOf<HttpRequestException>());
            using (Assert.EnterMultipleScope()) {
                Assert.That(handler.Requests, Has.Count.EqualTo(1));
                Assert.That(Directory.GetFiles(directory.Root), Is.Empty);
            }
        }
    }

    [Test]
    public async Task File_CancellationDuringReadDeletesPartial() {
        var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var cancellation = new CancellationTokenSource())
        using (var directory = new UpdateTestDirectory())
        using (var http = new HttpClient(new UpdateHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StreamContent(new Body(directory.Bytes[..1], async token => {
                reading.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return 0;
            }))
        }))) {
            var task = new UpdateDownload(http).File(directory.Update(), directory.Root, Mock.Of<IOperationProgress>(),
                cancellation.Token);
            try {
                await reading.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally {
                cancellation.Cancel();
            }
            Assert.That(async () => await task, Throws.InstanceOf<OperationCanceledException>());
            Assert.That(Directory.GetFiles(directory.Root), Is.Empty);
        }
    }

    private static HttpResponseMessage Response(string failure, byte[] bytes) {
        if (failure == "http") {
            return new(HttpStatusCode.InternalServerError);
        }
        if (failure == "length header") {
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes[..1]) };
        }

        Stream body = failure switch {
            "oversized" => new Body([.. bytes, 5], _ => throw new AssertionException("Read past the size limit.")),
            "truncated" => new Body(bytes[..1]),
            "wrong hash" => new Body([4, 3, 2, 1]),
            "read error" => new Body(bytes[..1], _ => throw new IOException("Test read failure.")),
            "stall" => new Body(bytes[..1], async token => {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return 0;
            }),
            _ => throw new ArgumentException(nameof(failure))
        };
        return new(HttpStatusCode.OK) { Content = new StreamContent(body) };
    }

    private sealed class Body : MemoryStream {
        private readonly Func<CancellationToken, ValueTask<int>> ContinueReading;

        public override bool CanSeek => false;

        internal Body(byte[] bytes, Func<CancellationToken, ValueTask<int>> continueReading = null) : base(bytes) {
            ContinueReading = continueReading;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
            if (Position == Length && ContinueReading is not null) {
                return ContinueReading(cancellationToken);
            }
            return base.ReadAsync(buffer, cancellationToken);
        }
    }
}
