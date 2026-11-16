using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
public sealed class FtpTransferLimiterTest {
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static FtpTransferLimiter Subject() {
        return new FtpTransferLimiter(attempts: 5, retryDelay: TimeSpan.Zero);
    }

    private static Func<Action<string>, CancellationToken, Task<Stream>> Transfer(params Attempt[] attempts) {
        var queue = new Queue<Attempt>(attempts);
        return (header, token) => {
            var attempt = queue.Dequeue();
            foreach (var line in attempt.Lines) {
                header(line);
            }
            return Task.FromResult<Stream>(new AttemptStream(attempt));
        };
    }

    private static async Task<string> ReadAll(Stream stream) {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    [Test]
    public async Task Open_ReturnsTheWholeTransfer() {
        var subject = Subject();

        await using var stream = await subject.Open(
            Transfer(Attempt.Succeeds("contents")),
            CancellationToken.None);

        Assert.That(await ReadAll(stream), Is.EqualTo("contents"));
    }

    [Test]
    public async Task Open_ReturnsAnEmptyTransfer() {
        var subject = Subject();

        await using var stream = await subject.Open(Transfer(Attempt.Succeeds("")), CancellationToken.None);

        Assert.That(await ReadAll(stream), Is.Empty);
    }

    [Test]
    public async Task Open_RetriesALoginRefusedWhileAnotherDownloadIsLoggedIn() {
        var subject = Subject();
        var running = Attempt.Succeeds("first");
        running.Gate = new TaskCompletionSource();
        var first = subject.Open(Transfer(running), CancellationToken.None);
        await running.Started.Task.WaitAsync(Timeout);

        var second = subject.Open(
            Transfer(Attempt.RefusedLogin(), Attempt.Succeeds("second")),
            CancellationToken.None);
        await WaitFor(() => subject.Limit == 1);

        Assert.That(second.IsCompleted, Is.False);
        running.Gate.SetResult();
        await using (var firstStream = await first.WaitAsync(Timeout)) {
            Assert.That(await ReadAll(firstStream), Is.EqualTo("first"));
        }
        await using var secondStream = await second.WaitAsync(Timeout);
        Assert.That(await ReadAll(secondStream), Is.EqualTo("second"));
        Assert.That(subject.Limit, Is.EqualTo(1));
    }

    [Test]
    public async Task Open_RetriesAConnectionDroppedBeforeLoginAfterALoginSucceeded() {
        var subject = Subject();
        await using (var first = await subject.Open(Transfer(Attempt.Succeeds("first")), CancellationToken.None)) {
            await ReadAll(first);
        }

        await using var second = await subject.Open(
            Transfer(Attempt.Dropped(), Attempt.Succeeds("second")),
            CancellationToken.None);

        Assert.That(await ReadAll(second), Is.EqualTo("second"));
    }

    [Test]
    public async Task Open_DoesNotRetryWhenNoLoginHasSucceeded() {
        var subject = Subject();
        var reads = 0;
        var transfer = Transfer(Attempt.RefusedLogin(), Attempt.Succeeds("unexpected"));

        await Assert.ThrowsAsync<IOException>(() => subject.Open(
            (header, token) => {
                reads++;
                return transfer(header, token);
            },
            CancellationToken.None));
        Assert.That(reads, Is.EqualTo(1));
    }

    [Test]
    public async Task Open_DoesNotRetryAFailureAfterLogin() {
        var subject = Subject();
        await using (var first = await subject.Open(Transfer(Attempt.Succeeds("first")), CancellationToken.None)) {
            await ReadAll(first);
        }

        await Assert.ThrowsAsync<IOException>(() => subject.Open(
            Transfer(Attempt.FailsAfterLogin(), Attempt.Succeeds("unexpected")),
            CancellationToken.None));
    }

    [Test]
    public async Task Open_ReportsEveryFailureWhenNoConcurrentLoginSucceeds() {
        var subject = Subject();
        var firstAttempt = Attempt.RefusedLogin();
        firstAttempt.Gate = new TaskCompletionSource();
        var secondAttempt = Attempt.RefusedLogin();
        secondAttempt.Gate = new TaskCompletionSource();
        var first = subject.Open(Transfer(firstAttempt), CancellationToken.None);
        var second = subject.Open(Transfer(secondAttempt), CancellationToken.None);
        await firstAttempt.Started.Task.WaitAsync(Timeout);
        await secondAttempt.Started.Task.WaitAsync(Timeout);

        firstAttempt.Gate.SetResult();
        await Task.Delay(50);
        Assert.That(first.IsCompleted, Is.False);
        secondAttempt.Gate.SetResult();

        await Assert.ThrowsAsync<IOException>(() => first.WaitAsync(Timeout));
        await Assert.ThrowsAsync<IOException>(() => second.WaitAsync(Timeout));
        Assert.That(subject.Limit, Is.EqualTo(int.MaxValue));
    }

    [Test]
    public async Task Open_DoesNotRetryWhenCanceled() {
        var subject = Subject();
        await using (var first = await subject.Open(Transfer(Attempt.Succeeds("first")), CancellationToken.None)) {
            await ReadAll(first);
        }
        using var cancellation = new CancellationTokenSource();
        var attempt = Attempt.RefusedLogin();
        attempt.Gate = new TaskCompletionSource();
        var open = subject.Open(Transfer(attempt, Attempt.Succeeds("unexpected")), cancellation.Token);
        await attempt.Started.Task.WaitAsync(Timeout);

        cancellation.Cancel();

        await Assert.CatchAsync<OperationCanceledException>(() => open.WaitAsync(Timeout));
    }

    [Test]
    public async Task Open_ReleasesTheSlotWhenTheStreamIsDisposed() {
        var subject = Subject();
        await using (var first = await subject.Open(Transfer(Attempt.Succeeds("first")), CancellationToken.None)) {
            await ReadAll(first);
        }
        var running = Attempt.Succeeds("running");
        running.Gate = new TaskCompletionSource();
        var held = subject.Open(Transfer(running), CancellationToken.None);
        await running.Started.Task.WaitAsync(Timeout);
        var refused = subject.Open(
            Transfer(Attempt.RefusedLogin(), Attempt.Succeeds("next")),
            CancellationToken.None);
        await WaitFor(() => subject.Limit == 1);
        running.Gate.SetResult();
        var heldStream = await held.WaitAsync(Timeout);

        await Task.Delay(50);
        Assert.That(refused.IsCompleted, Is.False);
        await heldStream.DisposeAsync();

        await using var next = await refused.WaitAsync(Timeout);
        Assert.That(await ReadAll(next), Is.EqualTo("next"));
    }

    private static async Task WaitFor(Func<bool> condition) {
        var deadline = DateTime.UtcNow + Timeout;
        while (condition() is false) {
            if (DateTime.UtcNow > deadline) {
                Assert.Fail("The expected condition was not met.");
            }
            await Task.Delay(10);
        }
    }

    private sealed class Attempt {
        public IReadOnlyList<string> Lines { get; private init; } = [];
        public byte[] Data { get; private init; } = [];
        public Exception Failure { get; private init; }
        public TaskCompletionSource Gate { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static Attempt Succeeds(string contents) {
            return new Attempt {
                Lines = ["220 Service ready", "331 Password required", "230 User logged in.", "226 Transfer complete."],
                Data = Encoding.UTF8.GetBytes(contents)
            };
        }

        public static Attempt RefusedLogin() {
            return new Attempt {
                Lines = ["220 Service ready", "331 Password required", "530 User cannot log in."],
                Failure = new IOException("Access denied: 530")
            };
        }

        public static Attempt Dropped() {
            return new Attempt {
                Failure = new IOException("response reading failed")
            };
        }

        public static Attempt FailsAfterLogin() {
            return new Attempt {
                Lines = ["220 Service ready", "230 User logged in.", "550 File unavailable."],
                Failure = new IOException("The file is unavailable.")
            };
        }
    }

    private sealed class AttemptStream : Stream {
        private readonly Attempt Attempt;
        private int Offset;

        public AttemptStream(Attempt attempt) {
            Attempt = attempt;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => Attempt.Data.Length;

        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
                                                       CancellationToken cancellationToken = default) {
            Attempt.Started.TrySetResult();
            if (Attempt.Gate is not null) {
                await Attempt.Gate.Task.WaitAsync(cancellationToken);
            }
            if (Attempt.Failure is not null) {
                throw Attempt.Failure;
            }
            var count = Math.Min(buffer.Length, Attempt.Data.Length - Offset);
            Attempt.Data.AsMemory(Offset, count).CopyTo(buffer);
            Offset += count;
            return count;
        }

        public override int Read(byte[] buffer, int offset, int count) {
            return ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        }

        public override void Flush() {
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }
    }
}
