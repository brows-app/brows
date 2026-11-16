using Brows.Composition;
using Brows.Entries;
using Brows.IO;
using Brows.Operations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class FileSystemProviderTest {
    private FileSysSandbox Sandbox;
    private DirectoryInfo TargetDirectory;
    private FileSystemProvider TargetProvider;
    private TestOperationProgress Progress;

    private Task<bool> Copy(string relativePath, string contents, bool isDirectory = false) {
        return Copy(new TestEntryStreamSource(relativePath, contents, isDirectory));
    }

    private Task<bool> Copy(params IEntryStreamSource[] sources) {
        var io = new ProvidedIO {
            StreamSets = new IEntryStreamSet[] {
                new TestEntryStreamSet(sources)
            }
        };
        return new FileSystemProvider.CopyProvidedIO().Work(
            new[] { io }, TargetProvider, Progress, CancellationToken.None);
    }

    [SetUp]
    public void SetUp() {
        Sandbox = new();
        TargetDirectory = Directory.CreateDirectory(Path.Combine(Sandbox.Root, "target"));
        TargetProvider = new FileSystemProvider(
            new FileSystemProviderFactory(), TargetDirectory,
            caseSensitive: false, initialCapacity: 0);
        Progress = new();
    }

    [TearDown]
    public void TearDown() {
        TargetProvider.Dispose();
        Sandbox.Dispose();
    }

    [Test]
    public async Task CopyProvidedIO_RejectsParentTraversalBeforeWritingOutsideTarget() {
        var outsidePath = Path.Combine(Sandbox.Root, "outside.txt");

        await Assert.ThrowsAsync<InvalidDataException>(() => Copy("../outside.txt", "outside"));

        Assert.That(File.Exists(outsidePath), Is.False);
    }

    [Test]
    public async Task CopyProvidedIO_CopiesNestedFileWithinTarget() {
        var copied = await Copy("nested/file.txt", "contents");
        var copiedPath = Path.Combine(TargetDirectory.FullName, "nested", "file.txt");

        using (Assert.EnterMultipleScope()) {
            Assert.That(copied, Is.True);
            Assert.That(File.ReadAllText(copiedPath), Is.EqualTo("contents"));
        }
    }

    [Test]
    public async Task CopyProvidedIO_ReportsFileSizeAcrossSeparateFiles() {
        var first = new TestEntryStreamSource("first.txt", "first", false);
        var second = new TestEntryStreamSource("second.txt", "second file", false);

        await Copy(first, second);

        using (Assert.EnterMultipleScope()) {
            Assert.That(Progress.Kind, Is.EqualTo(OperationProgressKind.FileSize));
            Assert.That(Progress.Target, Is.EqualTo(16));
            Assert.That(Progress.Value, Is.EqualTo(16));
        }
    }

    [Test]
    public async Task CopyProvidedIO_CopiesDirectoryWithTrailingSeparator() {
        const string directoryName = "Star Wars Episode II - Attack of the Clones (2002)";
        var copied = await Copy(directoryName + "/", contents: null, isDirectory: true);
        var copiedPath = Path.Combine(TargetDirectory.FullName, directoryName);

        using (Assert.EnterMultipleScope()) {
            Assert.That(copied, Is.True);
            Assert.That(Directory.Exists(copiedPath), Is.True);
        }
    }

    [TestCase("C:outside.txt")]
    [TestCase("C:\\outside.txt")]
    [TestCase("/outside.txt")]
    [TestCase("\\\\server\\share\\outside.txt")]
    [TestCase("folder/name:stream.txt")]
    [TestCase("CON.txt")]
    [TestCase("COM1.txt")]
    [TestCase("LPT\u00B9.txt")]
    [TestCase("name.")]
    [TestCase("name ")]
    [TestCase(" ")]
    [TestCase("")]
    [TestCase("folder//")]
    [TestCase("folder//child.txt")]
    public async Task CopyProvidedIO_RejectsWindowsPathAliasesBeforeWriting(string relativePath) {
        await Assert.ThrowsAsync<InvalidDataException>(() => Copy(relativePath, "contents"));

        Assert.That(Directory.GetFileSystemEntries(TargetDirectory.FullName), Is.Empty);
    }

    [Test]
    public async Task CopyProvidedIO_CopiesUnicodeWhitespaceName() {
        Assert.That(await Copy("\u2003", "contents"), Is.True);
        Assert.That(File.ReadAllText(Path.Combine(TargetDirectory.FullName, "\u2003")), Is.EqualTo("contents"));
    }

    [Test]
    public async Task CopyProvidedIO_EmptySelectionDoesNotReportSuccess() {
        Assert.That(await Copy(Array.Empty<IEntryStreamSource>()), Is.False);
    }

    [Test]
    public async Task CopyProvidedIO_InvalidNameDoesNotPublishOtherStreams() {
        await Assert.ThrowsAsync<InvalidDataException>(() => Copy(
            new TestEntryStreamSource("valid.txt", "contents", false),
            new TestEntryStreamSource(" ", "contents", false)));
        Assert.That(Directory.GetFileSystemEntries(TargetDirectory.FullName), Is.Empty);
    }

    [Test]
    public async Task CopyProvidedIO_InvalidLaterStreamSetKeepsEarlierFiles() {
        var input = new[] {
            new ProvidedIO {
                StreamSets = [new TestEntryStreamSet([new TestEntryStreamSource("valid.txt", "contents", false)])]
            },
            new ProvidedIO {
                StreamSets = [new TestEntryStreamSet([new TestEntryStreamSource(" ", "contents", false)])]
            }
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => new FileSystemProvider.CopyProvidedIO().Work(
            input, TargetProvider, Progress, CancellationToken.None));
        Assert.That(
            Directory.GetFileSystemEntries(TargetDirectory.FullName),
            Is.EqualTo(new[] { Path.Combine(TargetDirectory.FullName, "valid.txt") }));
    }

    [Test]
    public async Task CopyProvidedIO_SourceWithoutContentIsRejected() {
        var copied = await Copy(new TestEntryStreamSource("file.txt", null));

        Assert.That(copied, Is.False);
        Assert.That(Directory.GetFileSystemEntries(TargetDirectory.FullName), Is.Empty);
    }

    [Test]
    public async Task CopyProvidedIO_RejectsReparsePointBelowTarget() {
        var outsideDirectory = Directory.CreateDirectory(Path.Combine(Sandbox.Root, "outside"));
        var linkPath = Path.Combine(TargetDirectory.FullName, "linked");
        try {
            Directory.CreateSymbolicLink(linkPath, outsideDirectory.FullName);
        }
        catch (Exception error) {
            var cannotCreateSymbolicLink = error is UnauthorizedAccessException or IOException or
                PlatformNotSupportedException;
            if (!cannotCreateSymbolicLink) {
                throw;
            }
            Assert.Ignore($"The test environment cannot create a directory symbolic link: {error.Message}");
        }

        try {
            await Assert.ThrowsAsync<IOException>(() => Copy("linked/outside.txt", "contents"));
            Assert.That(Directory.GetFileSystemEntries(outsideDirectory.FullName), Is.Empty);
        }
        finally {
            if (Directory.Exists(linkPath)) {
                Directory.Delete(linkPath);
            }
        }
    }

    [Test]
    public async Task CopyProvidedIO_ContinuesWithSiblingFilesWhenAStreamFails() {
        var firstContents = System.Text.Encoding.UTF8.GetBytes("first");
        var first = new TestEntryStreamSource("first.txt", new MemoryStream(firstContents));
        var failing = new TestEntryStreamSource("failing.txt", new FailingReadStream("partial"));
        var last = new TestEntryStreamSource("last.txt", "last", false);

        var copied = await Copy(first, failing, last);

        using (Assert.EnterMultipleScope()) {
            Assert.That(copied, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(TargetDirectory.FullName, "first.txt")), Is.EqualTo("first"));
            Assert.That(File.Exists(Path.Combine(TargetDirectory.FullName, "failing.txt")), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(TargetDirectory.FullName, "last.txt")), Is.EqualTo("last"));
            Assert.That(Directory.GetFiles(TargetDirectory.FullName, ".brows-*.tmp", SearchOption.AllDirectories),
                Is.Empty);
        }
    }

    [Test]
    public async Task CopyProvidedIO_ContinuesWithSiblingFilesWhenStreamReadinessFails() {
        var first = new TestEntryStreamSource("first.txt", "first", false);
        var failingStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("unavailable"));
        var failing = new TestEntryStreamSource(
            "failing.txt",
            failingStream,
            new IOException("The test stream failed to become ready."));
        var last = new TestEntryStreamSource("last.txt", "last", false);

        var copied = await Copy(first, failing, last);

        using (Assert.EnterMultipleScope()) {
            Assert.That(copied, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(TargetDirectory.FullName, "first.txt")), Is.EqualTo("first"));
            Assert.That(File.Exists(Path.Combine(TargetDirectory.FullName, "failing.txt")), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(TargetDirectory.FullName, "last.txt")), Is.EqualTo("last"));
            Assert.That(failingStream.CanRead, Is.False);
            Assert.That(Directory.GetFiles(TargetDirectory.FullName, ".brows-*.tmp", SearchOption.AllDirectories),
                Is.Empty);
        }
    }

    [Test]
    public async Task CopyProvidedIO_DoesNotPublishFileWhenSourceDisposalFails() {
        var first = new TestEntryStreamSource("first.txt", "first", false);
        var failingStream = new FailingDisposeStream(System.Text.Encoding.UTF8.GetBytes("complete contents"));
        var failing = new TestEntryStreamSource("failing.txt", failingStream);
        var last = new TestEntryStreamSource("last.txt", "last", false);

        var copied = await Copy(first, failing, last);

        using (Assert.EnterMultipleScope()) {
            Assert.That(copied, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(TargetDirectory.FullName, "first.txt")), Is.EqualTo("first"));
            Assert.That(File.Exists(Path.Combine(TargetDirectory.FullName, "failing.txt")), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(TargetDirectory.FullName, "last.txt")), Is.EqualTo("last"));
            Assert.That(failingStream.CanRead, Is.False);
            Assert.That(Directory.GetFiles(TargetDirectory.FullName, ".brows-*.tmp", SearchOption.AllDirectories),
                Is.Empty);
        }
    }

    [Test]
    public async Task CopyProvidedIO_DoesNotCreateDirectoryWhenReadinessDisposalFails() {
        var first = new TestEntryStreamSource("first.txt", "first", false);
        var failing = new TestEntryStreamSource(
            "failing/",
            contents: null,
            isDirectory: true,
            streamReadyDisposalFailure: new IOException("The test stream readiness failed to close."));
        var last = new TestEntryStreamSource("last.txt", "last", false);

        var copied = await Copy(first, failing, last);

        using (Assert.EnterMultipleScope()) {
            Assert.That(copied, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(TargetDirectory.FullName, "first.txt")), Is.EqualTo("first"));
            Assert.That(Directory.Exists(Path.Combine(TargetDirectory.FullName, "failing")), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(TargetDirectory.FullName, "last.txt")), Is.EqualTo("last"));
        }
    }

    [Test]
    public async Task CopyProvidedIO_CancellationKeepsCompletedFiles() {
        var first = new TestEntryStreamSource("first.txt", "first", false);
        var second = new TestEntryStreamSource("second.txt", "second", false);
        var io = new ProvidedIO {
            StreamSets = [new TestEntryStreamSet([first, second])]
        };
        using var cancellation = new CancellationTokenSource();
        var progress = new TestOperationProgress(cancellation, "second.txt");

        await Assert.ThrowsAsync<OperationCanceledException>(() => new FileSystemProvider.CopyProvidedIO().Work(
            [io], TargetProvider, progress, cancellation.Token));

        var firstPath = Path.Combine(TargetDirectory.FullName, "first.txt");
        Assert.That(Directory.GetFileSystemEntries(TargetDirectory.FullName), Is.EqualTo(new[] { firstPath }));
        Assert.That(File.ReadAllText(firstPath), Is.EqualTo("first"));
    }

    [Test]
    public async Task CopyProvidedIO_KeepsCompletedFilesWhenLaterStreamFailsWithoutProgress() {
        var firstContents = System.Text.Encoding.UTF8.GetBytes("first");
        var first = new TestEntryStreamSource("first.txt", new MemoryStream(firstContents));
        var failing = new TestEntryStreamSource("failing.txt", new FailingReadStream("partial"));
        var io = new ProvidedIO {
            StreamSets = [new TestEntryStreamSet([first, failing])]
        };

        await Assert.ThrowsAsync<IOException>(() => new FileSystemProvider.CopyProvidedIO().Work(
            [io], TargetProvider, progress: null, CancellationToken.None));

        Assert.That(
            Directory.GetFileSystemEntries(TargetDirectory.FullName),
            Is.EqualTo(new[] { Path.Combine(TargetDirectory.FullName, "first.txt") }));
    }

    [Test]
    public async Task CopyProvidedIO_PublishesEachFileBeforeTheNextSourceIsCopied() {
        var firstPath = Path.Combine(TargetDirectory.FullName, "first.txt");
        var firstWasPublished = false;
        var first = new TestEntryStreamSource("first.txt", "first", false);
        var second = new TestEntryStreamSource(
            "second.txt",
            new MemoryStream(System.Text.Encoding.UTF8.GetBytes("second")),
            onStreamReady: () => firstWasPublished = File.Exists(firstPath));

        var copied = await Copy(first, second);

        Assert.That(copied, Is.True);
        Assert.That(firstWasPublished, Is.True);
    }

    [Test]
    public async Task CopyProvidedIO_CopiesFilesAsChildrenOfTheirDirectory() {
        var copied = await Copy(
            new TestEntryStreamSource("folder/", null, isDirectory: true),
            new TestEntryStreamSource("top.txt", "top", false),
            new TestEntryStreamSource("folder/nested/", null, isDirectory: true),
            new TestEntryStreamSource("folder/inner.txt", "inner", false),
            new TestEntryStreamSource("folder/nested/deep.txt", "deep", false));

        var folder = Progress.ChildOperations.Single(child => child.Name == "folder/");
        var nested = folder.ChildOperations.Single(child => child.Name == "nested");
        using (Assert.EnterMultipleScope()) {
            Assert.That(copied, Is.True);
            Assert.That(Progress.ChildOperations.Select(child => child.Name), Is.EqualTo(new[] { "folder/", "top.txt" }));
            Assert.That(folder.ChildOperations.Select(child => child.Name), Is.EqualTo(new[] { "nested", "inner.txt" }));
            Assert.That(nested.ChildOperations.Select(child => child.Name), Is.EqualTo(new[] { "deep.txt" }));
            Assert.That(
                File.ReadAllText(Path.Combine(TargetDirectory.FullName, "folder", "nested", "deep.txt")),
                Is.EqualTo("deep"));
        }
    }

    [TestCase(1, 1)]
    [TestCase(2, 2)]
    [TestCase(3, 3)]
    [TestCase(0, 1)]
    [TestCase(-1, 1)]
    public async Task CopyProvidedIO_CopiesStreamSourcesConcurrentlyUpToTheSetLimit(int concurrency, int expected) {
        var tracker = new ConcurrencyTracker(expected);
        var sources = Enumerable.Range(0, 5)
            .Select(i => new TestEntryStreamSource($"file{i}.txt", new TrackedStream(tracker, $"contents{i}")))
            .ToArray();
        var io = new ProvidedIO {
            StreamSets = [new TestEntryStreamSet(sources, concurrency)]
        };

        var copied = await new FileSystemProvider.CopyProvidedIO().Work(
            [io], TargetProvider, Progress, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(copied, Is.True);
            Assert.That(tracker.Maximum, Is.EqualTo(expected));
            for (var i = 0; i < sources.Length; i++) {
                Assert.That(
                    File.ReadAllText(Path.Combine(TargetDirectory.FullName, $"file{i}.txt")),
                    Is.EqualTo($"contents{i}"));
            }
            Assert.That(Directory.GetFiles(TargetDirectory.FullName, ".brows-*.tmp", SearchOption.AllDirectories),
                Is.Empty);
        }
    }

    [Test]
    public async Task CopyProvidedIO_CancellationDuringConcurrentCopiesKeepsCompletedFiles() {
        using var cancellation = new CancellationTokenSource();
        var donePath = Path.Combine(TargetDirectory.FullName, "done.txt");
        var blocked = new BlockingStream();
        var sources = new IEntryStreamSource[] {
            new TestEntryStreamSource("blocked.txt", blocked),
            new TestEntryStreamSource("done.txt", "done", false)
        };
        var io = new ProvidedIO {
            StreamSets = [new TestEntryStreamSet(sources, 2)]
        };

        var work = new FileSystemProvider.CopyProvidedIO().Work(
            [io], TargetProvider, Progress, cancellation.Token);
        await blocked.Reading.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitFor(() => File.Exists(donePath));
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => work);
        using (Assert.EnterMultipleScope()) {
            Assert.That(Directory.GetFileSystemEntries(TargetDirectory.FullName), Is.EqualTo(new[] { donePath }));
            Assert.That(File.ReadAllText(donePath), Is.EqualTo("done"));
        }
    }

    private static async Task WaitFor(Func<bool> condition) {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (condition() is false) {
            if (DateTime.UtcNow > deadline) {
                Assert.Fail("The expected condition was not met.");
            }
            await Task.Delay(10);
        }
    }

    private sealed class TestEntryStreamSet : EntryStreamSet, IEntryStreamSet {
        private readonly IReadOnlyList<IEntryStreamSource> Sources;

        protected override IEnumerable<IEntryStreamSource> StreamSource() {
            return Sources;
        }

        public int StreamSourceConcurrency { get; }

        public TestEntryStreamSet(IEnumerable<IEntryStreamSource> sources, int streamSourceConcurrency = 1) {
            Sources = sources.ToList();
            StreamSourceConcurrency = streamSourceConcurrency;
        }
    }

    private sealed class ConcurrencyTracker {
        private readonly Lock Locker = new();
        private readonly TaskCompletionSource Saturated = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly int Limit;
        private int Active;

        public int Maximum { get; private set; }

        public ConcurrencyTracker(int limit) {
            Limit = limit;
        }

        public async Task Enter() {
            lock (Locker) {
                Active++;
                Maximum = Math.Max(Maximum, Active);
                if (Active >= Limit) {
                    Saturated.TrySetResult();
                }
            }
            await Saturated.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        public void Exit() {
            lock (Locker) {
                Active--;
            }
        }
    }

    private sealed class TrackedStream : MemoryStream {
        private readonly ConcurrencyTracker Tracker;
        private bool Entered;
        private bool Exited;

        public TrackedStream(ConcurrencyTracker tracker, string contents)
            : base(System.Text.Encoding.UTF8.GetBytes(contents)) {
            Tracker = tracker;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
                                                       CancellationToken cancellationToken = default) {
            if (Entered is false) {
                Entered = true;
                await Tracker.Enter();
            }
            var read = await base.ReadAsync(buffer, cancellationToken);
            if (read == 0 && Exited is false) {
                Exited = true;
                Tracker.Exit();
            }
            return read;
        }
    }

    private sealed class BlockingStream : MemoryStream {
        public TaskCompletionSource Reading { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
                                                       CancellationToken cancellationToken = default) {
            Reading.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }

    private sealed class TestOperationProgress : IOperationProgress {
        private readonly Lock Locker = new();
        private readonly TestOperationProgress Parent;
        private readonly CancellationTokenSource Cancellation;
        private readonly string CancelOnChildName;
        private readonly List<TestOperationProgress> ChildList = [];

        public OperationProgressKind? Kind { get; private set; }
        public long Value { get; private set; }
        public long Target { get; private set; }
        public string Name { get; }

        public IReadOnlyList<TestOperationProgress> ChildOperations {
            get {
                lock (Locker) {
                    return ChildList.ToList();
                }
            }
        }

        private TestOperationProgress(TestOperationProgress parent, string name) {
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            Name = name;
            Cancellation = parent.Cancellation;
            CancelOnChildName = parent.CancelOnChildName;
        }

        public TestOperationProgress(CancellationTokenSource cancellation = null, string cancelOnChildName = null) {
            Cancellation = cancellation;
            CancelOnChildName = cancelOnChildName;
        }

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
            long progressChange;
            long targetChange;
            lock (Locker) {
                var previousProgress = Value;
                var previousTarget = Target;
                Value = (setProgress ?? Value) + (addProgress ?? 0);
                Target = (setTarget ?? Target) + (addTarget ?? 0);
                Kind = kind ?? Kind;
                progressChange = Value - previousProgress;
                targetChange = Target - previousTarget;
            }
            if (progressChange != 0 || targetChange != 0) {
                Parent?.Change(addProgress: progressChange, addTarget: targetChange);
            }
        }

        public async Task<OperationResult> Child(
            string name,
            OperationDelegate task,
            OperationFailureBehavior failureBehavior,
            CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                ArgumentNullException.ThrowIfNull(task);
                if (string.Equals(name, CancelOnChildName, StringComparison.Ordinal)) {
                    Cancellation?.Cancel();
                }
                var childProgress = new TestOperationProgress(this, name);
                lock (Locker) {
                    ChildList.Add(childProgress);
                }
                var token = Cancellation?.Token ?? cancellationToken;
                await task(childProgress, token);
                cancellationToken.ThrowIfCancellationRequested();
                return new OperationResult(OperationResultKind.Succeeded, null);
            }
            catch (Exception error) {
                cancellationToken.ThrowIfCancellationRequested();
                if (failureBehavior == OperationFailureBehavior.Throw) {
                    throw;
                }
                var kind = error is OperationCanceledException ? OperationResultKind.Canceled : OperationResultKind.Failed;
                return new OperationResult(kind, error);
            }
        }

        public Task<OperationBatchResult> Children<T>(
            IEnumerable<T> source,
            Func<T, OperationChild> child,
            OperationFailureBehavior failureBehavior,
            CancellationToken cancellationToken) {
            throw new NotSupportedException();
        }
    }

    private sealed class TestEntryStreamSource : IEntryStreamSource {
        private readonly Exception StreamReadyFailure;
        private readonly Exception StreamReadyDisposalFailure;
        private readonly Action OnStreamReady;

        public string EntryID => RelativePath;
        public string EntryName => RelativePath;
        public string RelativePath { get; }
        public string SourceFile => null;
        public string SourceDirectory => IsDirectory ? "source" : null;
        public long StreamLength => Stream?.Length ?? 0;
        public Stream Stream { get; }

        private bool IsDirectory { get; }

        public TestEntryStreamSource(
            string relativePath,
            string contents,
            bool isDirectory,
            Exception streamReadyDisposalFailure = null) : this(
            relativePath,
            isDirectory ? null : new MemoryStream(System.Text.Encoding.UTF8.GetBytes(contents)),
            streamReadyDisposalFailure: streamReadyDisposalFailure) {
            IsDirectory = isDirectory;
        }

        public TestEntryStreamSource(
            string relativePath,
            Stream stream,
            Exception streamReadyFailure = null,
            Exception streamReadyDisposalFailure = null,
            Action onStreamReady = null) {
            RelativePath = relativePath;
            Stream = stream;
            StreamReadyFailure = streamReadyFailure;
            StreamReadyDisposalFailure = streamReadyDisposalFailure;
            OnStreamReady = onStreamReady;
        }

        public Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            OnStreamReady?.Invoke();
            if (StreamReadyFailure is not null) {
                return Task.FromException<IEntryStreamReady>(StreamReadyFailure);
            }
            if (StreamReadyDisposalFailure is not null) {
                return Task.FromResult<IEntryStreamReady>(new FailingEntryStreamReady(StreamReadyDisposalFailure));
            }
            return Task.FromResult<IEntryStreamReady>(new EntryStreamReady());
        }
    }

    private sealed class FailingEntryStreamReady : IEntryStreamReady {
        private readonly Exception Failure;

        public FailingEntryStreamReady(Exception failure) {
            Failure = failure;
        }

        public void Dispose() {
            throw Failure;
        }
    }

    private sealed class FailingReadStream : Stream {
        private readonly byte[] Contents;
        private int Reads;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => Contents.Length;
        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public FailingReadStream(string contents) {
            Contents = System.Text.Encoding.UTF8.GetBytes(contents);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            if (Reads++ > 0) {
                return Task.FromException<int>(new IOException("The test stream failed after partial output."));
            }
            var length = Math.Min(Contents.Length / 2, count);
            Array.Copy(Contents, 0, buffer, offset, length);
            return Task.FromResult(length);
        }

        public override int Read(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }

        public override void Flush() {
            throw new NotSupportedException();
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

    private sealed class FailingDisposeStream : MemoryStream {
        public FailingDisposeStream(byte[] contents) : base(contents) {
        }

        public override async ValueTask DisposeAsync() {
            await base.DisposeAsync();
            throw new IOException("The source stream failed to close.");
        }
    }
}
