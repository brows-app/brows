using Brows.Operations;
using Brows.Support;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

[TestFixture]
internal sealed class FindWorkTest {
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    private static FileSystemFindResult Result(DirectoryInfo root, Find.Parameter parameter) {
        return new FileSystemFindResult(root, parameter, parameter.Pattern, null);
    }

    private static FoundInInfo[] Rows(FileSystemFindResult result) {
        return ((IEnumerable)result.Source).Cast<FoundInInfo>().ToArray();
    }

    [Test]
    public async Task Done_CountsAFileOnceAcrossNameAndMultipleTextMatches() {
        using var fixture = new TempDirectory();
        fixture.CreateFile("needle.txt", "needle\r\nneedle");
        var result = Result(fixture.Root, new Find.Parameter {
            Pattern = "needle", In = FindIn.FileName | FindIn.FileText
        });
        var progress = new Progress();

        await new FindWork(result).Done(progress, default).WaitAsync(Deadline);

        Assert.That(result.MatchTried, Is.EqualTo(1));
        Assert.That(result.MatchMatched, Is.EqualTo(1));
        Assert.That(Rows(result), Has.Length.EqualTo(3));
        Assert.That(progress.Value, Is.EqualTo(1));
        Assert.That(progress.Target, Is.EqualTo(1));
        Assert.That(result.Complete, Is.True);
        Assert.That(result.Exception, Is.Null);
    }

    [Test]
    public async Task Done_AdvancesFilteredItemsWithoutInflatingCheckedCount() {
        using var fixture = new TempDirectory();
        fixture.CreateFile("a.txt");
        fixture.CreateFile("b.log");
        fixture.CreateFile("c.log");
        fixture.CreateFile("d.log");
        var result = Result(fixture.Root, new Find.Parameter { Pattern = "*", Include = ["*.txt"] });
        var progress = new Progress();

        await new FindWork(result).Done(progress, default).WaitAsync(Deadline);

        Assert.That(result.MatchTried, Is.EqualTo(1));
        Assert.That(result.MatchMatched, Is.EqualTo(1));
        Assert.That(progress.Value, Is.EqualTo(4));
        Assert.That(progress.Target, Is.EqualTo(4));
    }

    [Test]
    public async Task Done_ReportsLockedFileAsErrorRatherThanMatch() {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("locked.txt", "needle");
        using var locked = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var result = Result(fixture.Root, new Find.Parameter { Pattern = "absent", In = FindIn.FileText });

        await new FindWork(result).Done(null, default).WaitAsync(Deadline);

        Assert.That(result.MatchTried, Is.EqualTo(1));
        Assert.That(result.MatchMatched, Is.Zero);
        Assert.That(result.ErrorCount, Is.EqualTo(1));
        Assert.That(Rows(result).Single(), Is.TypeOf<FoundInFileError>());
        Assert.That(result.Failed, Is.False);
    }

    [Test]
    public async Task Done_ReportsRegexTimeoutAndContinuesProcessingEntries() {
        using var fixture = new TempDirectory();
        fixture.CreateFile("timeout.txt");
        fixture.CreateFile("other.txt");
        var result = Result(fixture.Root, new Find.Parameter { Pattern = "*", Workers = 1 });
        var processed = 0;
        async Task<FindEntryResult> Process(FileSystemInfo entry, CancellationToken token) {
            await Task.CompletedTask;
            Interlocked.Increment(ref processed);
            if (entry.Name == "timeout.txt") {
                throw new RegexMatchTimeoutException(entry.Name, "pattern", TimeSpan.FromSeconds(1));
            }
            return new FindEntryResult(entry, []);
        }

        await new FindWork(result, Process).Done(null, default).WaitAsync(Deadline);

        Assert.That(processed, Is.EqualTo(2));
        Assert.That(result.ErrorCount, Is.EqualTo(1));
        Assert.That(Rows(result).Single(), Is.TypeOf<FoundInFileError>());
        Assert.That(result.Failed, Is.False);
        Assert.That(result.Complete, Is.True);
    }

    [Test]
    public async Task Done_ReportsMissingRootAsFatalFailure() {
        using var fixture = new TempDirectory();
        var missing = new DirectoryInfo(Path.Combine(fixture.Root.FullName, "missing"));
        var result = Result(missing, new Find.Parameter { Pattern = "*" });

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => new FindWork(result).Done(null, default).WaitAsync(Deadline));

        Assert.That(result.Complete, Is.True);
        Assert.That(result.Failed, Is.True);
        Assert.That(result.Exception, Is.TypeOf<DirectoryNotFoundException>());
    }

    [Test]
    public async Task Done_SkipsOversizedContentButStillFindsFileName() {
        using var fixture = new TempDirectory();
        fixture.CreateFile("needle.txt", "needle content exceeds the limit");
        var result = Result(fixture.Root, new Find.Parameter {
            Pattern = "needle", In = FindIn.FileName | FindIn.FileText, MaximumContentBytes = 4
        });

        await new FindWork(result).Done(null, default).WaitAsync(Deadline);

        Assert.That(result.SkippedCount, Is.EqualTo(1));
        Assert.That(result.ErrorCount, Is.Zero);
        Assert.That(result.MatchMatched, Is.EqualTo(1));
        Assert.That(Rows(result).Single(), Is.TypeOf<FoundInFileName>());
    }

    [Test]
    public async Task Done_BoundsPerFileRowsAndPreviews() {
        using var fixture = new TempDirectory();
        fixture.CreateFile("many.txt", string.Join('\n', Enumerable.Repeat("needle", 100)));
        var result = Result(fixture.Root, new Find.Parameter {
            Pattern = "needle", In = FindIn.FileText, MaximumMatchesPerFile = 3, MaximumPreviewLength = 2
        });

        await new FindWork(result).Done(null, default).WaitAsync(Deadline);

        var rows = Rows(result).Cast<FoundInFileText>().ToArray();
        Assert.That(rows, Has.Length.EqualTo(3));
        Assert.That(rows.Select(row => row.LineText), Is.All.EqualTo("ne"));
        Assert.That(result.MatchMatched, Is.EqualTo(1));
        Assert.That(result.Truncated, Is.True);
    }

    [Test]
    public async Task Done_BoundsGlobalRowsAndContinuesCountingFiles() {
        using var fixture = new TempDirectory();
        for (var i = 0; i < 20; i++) {
            fixture.CreateFile($"needle{i}.txt");
        }
        var result = Result(fixture.Root, new Find.Parameter { Pattern = "needle", MaximumResults = 2 });

        await new FindWork(result).Done(null, default).WaitAsync(Deadline);

        Assert.That(Rows(result), Has.Length.EqualTo(2));
        Assert.That(result.MatchMatched, Is.EqualTo(20));
        Assert.That(result.MatchTried, Is.EqualTo(20));
        Assert.That(result.Truncated, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Done_DecodesBomAndFindsWindowsLineSuffix(bool utf16) {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("bom.txt");
        Encoding encoding = utf16 ? Encoding.Unicode : new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        File.WriteAllText(file.FullName, "first\r\nneedle\r\n", encoding);
        var result = Result(fixture.Root, new Find.Parameter { Pattern = "*needle", In = FindIn.FileText });

        await new FindWork(result).Done(null, default).WaitAsync(Deadline);

        var row = (FoundInFileText)Rows(result).Single();
        Assert.That(row.LineText, Is.EqualTo("needle"));
        Assert.That(row.Match.Line, Is.EqualTo(1));
    }

    [Test]
    public async Task Done_UsesAtMostConfiguredWorkers() {
        using var fixture = new TempDirectory();
        for (var i = 0; i < 100; i++) {
            fixture.CreateFile($"{i}.txt", "needle");
        }
        var parameter = new Find.Parameter { Pattern = "needle", In = FindIn.FileText, Workers = 2 };
        var result = Result(fixture.Root, parameter);
        var real = new FindInInfo(fixture.Root, parameter);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maximum = 0;
        async Task<FindEntryResult> Process(FileSystemInfo entry, CancellationToken token) {
            var current = Interlocked.Increment(ref active);
            for (; ; ) {
                var previous = Volatile.Read(ref maximum);
                if (current <= previous) {
                    break;
                }
                if (Interlocked.CompareExchange(ref maximum, current, previous) == previous) {
                    break;
                }
            }
            if (current == 2) {
                started.TrySetResult();
            }
            try {
                await release.Task.WaitAsync(token);
                return await real.Process(entry, token);
            }
            finally {
                Interlocked.Decrement(ref active);
            }
        }
        var done = new FindWork(result, Process).Done(null, default);
        try {
            await started.Task.WaitAsync(Deadline);
            Assert.That(Volatile.Read(ref active), Is.EqualTo(2));
            Assert.That(result.Complete, Is.False);
        }
        finally {
            release.TrySetResult();
        }
        await done.WaitAsync(Deadline);
        Assert.That(maximum, Is.EqualTo(2));
        Assert.That(active, Is.Zero);
        Assert.That(result.MatchMatched, Is.EqualTo(100));
    }

    [Test]
    public async Task Done_CancellationJoinsWorkerBeforeMarkingComplete() {
        using var fixture = new TempDirectory();
        fixture.CreateFile("one.txt", "needle");
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = Result(fixture.Root, new Find.Parameter { Pattern = "needle", Workers = 1 });
        var exited = false;
        async Task<FindEntryResult> Process(FileSystemInfo entry, CancellationToken token) {
            started.TrySetResult();
            try {
                await release.Task;
                token.ThrowIfCancellationRequested();
                return new FindEntryResult(entry, []);
            }
            finally {
                exited = true;
            }
        }
        var done = new FindWork(result, Process).Done(null, cancellation.Token);
        try {
            await started.Task.WaitAsync(Deadline);
            cancellation.Cancel();
            await Assert.ThrowsAsync<TimeoutException>(() => done.WaitAsync(TimeSpan.FromMilliseconds(100)));
            Assert.That(result.Complete, Is.False);
        }
        finally {
            release.TrySetResult();
        }
        await Assert.ThrowsAsync<OperationCanceledException>(() => done.WaitAsync(Deadline));
        Assert.That(exited, Is.True);
        Assert.That(result.Complete, Is.True);
        Assert.That(result.Canceled, Is.True);
        Assert.That(result.Exception, Is.Null);
    }

    [Test]
    public async Task Done_CancellationKeepsLateWorkerReadFailureFromBecomingFatal() {
        using var fixture = new TempDirectory();
        fixture.CreateFile("one.txt");
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = Result(fixture.Root, new Find.Parameter { Pattern = "*", Workers = 1 });
        async Task<FindEntryResult> Process(FileSystemInfo entry, CancellationToken token) {
            started.TrySetResult();
            try {
                await Task.Delay(Timeout.Infinite, token);
            }
            catch (OperationCanceledException) {
                throw new IOException("A read failed while cancellation was stopping the worker.");
            }
            return new FindEntryResult(entry, []);
        }
        var done = new FindWork(result, Process).Done(null, cancellation.Token);
        await started.Task.WaitAsync(Deadline);
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => done.WaitAsync(Deadline));

        Assert.That(result.Canceled, Is.True);
        Assert.That(result.Failed, Is.False);
        Assert.That(result.Exception, Is.Null);
    }

    [Test]
    public async Task Done_WorkerFailureCancelsAndJoinsSibling() {
        using var fixture = new TempDirectory();
        fixture.CreateFile("one.txt");
        fixture.CreateFile("two.txt");
        var siblingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var siblingCanceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = Result(fixture.Root, new Find.Parameter { Pattern = "*", Workers = 2 });
        var sequence = 0;
        var active = 0;
        var expected = new IOException("Injected worker failure");
        async Task<FindEntryResult> Process(FileSystemInfo entry, CancellationToken token) {
            Interlocked.Increment(ref active);
            try {
                if (Interlocked.Increment(ref sequence) == 1) {
                    await siblingStarted.Task.WaitAsync(Deadline, CancellationToken.None);
                    throw expected;
                }
                using var registration = token.Register(() => siblingCanceled.TrySetResult());
                siblingStarted.TrySetResult();
                await release.Task;
                token.ThrowIfCancellationRequested();
                return new FindEntryResult(entry, []);
            }
            finally {
                Interlocked.Decrement(ref active);
            }
        }
        var done = new FindWork(result, Process).Done(null, default);
        try {
            await siblingCanceled.Task.WaitAsync(Deadline);
            await Assert.ThrowsAsync<TimeoutException>(() => done.WaitAsync(TimeSpan.FromMilliseconds(100)));
            Assert.That(result.Complete, Is.False);
        }
        finally {
            release.TrySetResult();
        }
        var failure = await Assert.ThrowsAsync<IOException>(() => done.WaitAsync(Deadline));
        Assert.That(failure, Is.SameAs(expected));
        Assert.That(active, Is.Zero);
        Assert.That(result.Exception, Is.SameAs(expected));
        Assert.That(result.Complete, Is.True);
    }

    [Test]
    public async Task Done_ConsumerFailureStopsBlockedPipeline() {
        using var fixture = new TempDirectory();
        for (var i = 0; i < 200; i++) {
            fixture.CreateFile($"{i}.txt");
        }
        var result = Result(fixture.Root, new Find.Parameter { Pattern = "*", Workers = 2 });
        var expected = new InvalidOperationException("Injected collection subscriber failure");
        NotifyCollectionChangedEventHandler fail = (sender, args) => throw expected;
        ((INotifyCollectionChanged)result.Source).CollectionChanged += fail;
        try {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new FindWork(result).Done(null, default).WaitAsync(Deadline));
            Assert.That(failure, Is.SameAs(expected));
            Assert.That(result.Complete, Is.True);
            Assert.That(result.Failed, Is.True);
        }
        finally {
            ((INotifyCollectionChanged)result.Source).CollectionChanged -= fail;
        }
    }

    [Test]
    public async Task Done_FlushesPartialBatchWhileAnotherFileIsBlocked() {
        using var fixture = new TempDirectory();
        fixture.CreateFile("one.txt", "needle");
        fixture.CreateFile("two.txt", "needle");
        var parameter = new Find.Parameter { Pattern = "needle", In = FindIn.FileText, Workers = 2 };
        var result = Result(fixture.Root, parameter);
        var real = new FindInInfo(fixture.Root, parameter);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var notified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sequence = 0;
        NotifyCollectionChangedEventHandler added = (sender, args) => notified.TrySetResult();
        ((INotifyCollectionChanged)result.Source).CollectionChanged += added;
        async Task<FindEntryResult> Process(FileSystemInfo entry, CancellationToken token) {
            if (Interlocked.Increment(ref sequence) == 2) {
                await release.Task.WaitAsync(token);
            }
            return await real.Process(entry, token);
        }
        var done = new FindWork(result, Process).Done(null, default);
        try {
            await notified.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.That(result.Complete, Is.False);
        }
        finally {
            release.TrySetResult();
            ((INotifyCollectionChanged)result.Source).CollectionChanged -= added;
            await done.WaitAsync(Deadline);
        }
    }

    [Test]
    public async Task Done_SearchesExplicitJunctionWithLogicalResultPaths() {
        using var fixture = new TempDirectory();
        var target = fixture.CreateDirectory("target");
        fixture.CreateFile(Path.Combine("target", "needle.txt"));
        var junction = fixture.CreateJunction("root-link", target);
        var result = Result(junction, new Find.Parameter { Pattern = "needle" });

        await new FindWork(result).Done(null, default).WaitAsync(Deadline);

        Assert.That(Rows(result).Single().FullPath, Is.EqualTo(Path.Combine(junction.FullName, "needle.txt")));
        Assert.That(Rows(result).Single().RelativeDirectory, Is.Empty);
    }

    [Test]
    public async Task Done_TopDirectoryOnlyCombinesFileAndDirectoryNameChecks() {
        using var fixture = new TempDirectory();
        fixture.CreateFile("needle.txt");
        fixture.CreateFile(Path.Combine("needle-directory", "needle-nested.txt"));
        var result = Result(fixture.Root, new Find.Parameter {
            Pattern = "needle", In = FindIn.FileName | FindIn.DirectoryName | FindIn.TopDirectoryOnly
        });
        var progress = new Progress();

        await new FindWork(result).Done(progress, default).WaitAsync(Deadline);

        Assert.That(Rows(result).Select(row => row.Info.Name), Is.EquivalentTo(new[] {
            "needle.txt", "needle-directory"
        }));
        Assert.That(result.MatchTried, Is.EqualTo(2));
        Assert.That(result.MatchMatched, Is.EqualTo(2));
        Assert.That(progress.Value, Is.EqualTo(2));
        Assert.That(progress.Target, Is.EqualTo(2));
    }

    [TestCase(0, 1024, 2, 2, 2)]
    [TestCase(33, 1024, 2, 2, 2)]
    [TestCase(1, 0, 2, 2, 2)]
    [TestCase(1, 1024, 0, 2, 2)]
    [TestCase(1, 1024, 2, 0, 2)]
    [TestCase(1, 1024, 2, 2, 0)]
    public async Task Done_RejectsInvalidLimitsAsVisibleFailure(
        int workers, long bytes, int matches, int rows, int preview) {
        using var fixture = new TempDirectory();
        fixture.CreateFile("one.txt", "needle");
        var result = Result(fixture.Root, new Find.Parameter {
            Pattern = "needle", Workers = workers, MaximumContentBytes = bytes,
            MaximumMatchesPerFile = matches, MaximumResults = rows, MaximumPreviewLength = preview
        });

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => new FindWork(result).Done(null, default).WaitAsync(Deadline));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Complete, Is.True);
        Assert.That(Rows(result), Is.Empty);
    }

    [Test]
    public async Task Done_PreCanceledTokenStillReleasesAllDirectoryHandles() {
        using var fixture = new TempDirectory();
        fixture.CreateFile("one.txt");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = Result(fixture.Root, new Find.Parameter { Pattern = "*" });

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new FindWork(result).Done(null, cancellation.Token).WaitAsync(Deadline));

        Assert.That(result.Canceled, Is.True);
        Assert.That(result.Complete, Is.True);
        Assert.That(Rows(result), Is.Empty);
    }

    private sealed class Progress : IOperationProgress {
        private readonly Lock Locker = new();
        internal long Value { get; private set; }
        internal long Target { get; private set; }

        public void Change(long? addProgress = null,
                           long? setProgress = null,
                           long? addTarget = null,
                           long? setTarget = null,
                           string progressString = null,
                           string targetString = null,
                           string name = null,
                           string data = null,
                           OperationProgressKind? kind = null,
                           string progressFormat = null,
                           string targetFormat = null,
                           string progressRateFormat = null,
                           string progressRateAverageFormat = null,
                           System.IFormatProvider formatProvider = null) {
            lock (Locker) {
                Value = (setProgress ?? Value) + (addProgress ?? 0);
                Target = (setTarget ?? Target) + (addTarget ?? 0);
            }
        }

        public Task<OperationResult> Child(
            string name,
            OperationDelegate task,
            OperationFailureBehavior failureBehavior,
            CancellationToken cancellationToken) {
            throw new NotSupportedException();
        }

        public Task<OperationBatchResult> Children<T>(
            IEnumerable<T> source,
            Func<T, OperationChild> child,
            OperationFailureBehavior failureBehavior,
            CancellationToken cancellationToken) {
            throw new NotSupportedException();
        }
    }
}
