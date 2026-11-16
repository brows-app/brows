using Brows.Commands;
using Brows.Composition;
using Brows.Entries;
using Brows.Operations;
using Brows.Panels;
using Brows.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Tests;

internal sealed class TestConfig : FileProtocolConfig {
}

internal sealed class TestProvider : FileProtocolProvider<TestEntry, TestConfig> {
    private readonly CopyIO CopyService = new();
    private readonly ProvideIO ProvideService = new();
    private readonly Dictionary<string, IReadOnlyList<TestEntry>> Listings = new(StringComparer.Ordinal);
    private readonly List<Uri> ListedUris = [];
    private readonly List<TestEntry> ReadEntries = [];

    protected sealed override Task List(CancellationToken token) {
        Listed++;
        return Task.CompletedTask;
    }

    public int Listed { get; private set; }

    public IWorkProvidedIO CopyProvidedIO => CopyService;
    public IReadOnlyList<string> CopyDirectories => CopyService.Directories;
    public IReadOnlyDictionary<string, MemoryStream> CopyOutputs => CopyService.Outputs;
    public int CopyWrites => CopyService.Writes;

    public int FailCopyWriteAt {
        get => CopyService.FailWriteAt;
        set => CopyService.FailWriteAt = value;
    }

    public IProvideIO ProvideIOService => ProvideService;
    public TestRemoteFileSystem RemoteFileSystem { get; }
    public IReadOnlyList<Uri> ListRequests => ListedUris;
    public IReadOnlyList<TestEntry> EntriesRead => ReadEntries;

    public TestProvider(Uri uri) : this(uri, new TestRemoteFileSystem()) {
    }

    public TestProvider(Uri uri, TestRemoteFileSystem remoteFileSystem) : base(uri, 0, null, null) {
        RemoteFileSystem = remoteFileSystem ?? throw new ArgumentNullException(nameof(remoteFileSystem));
    }

    public TestProvider(string uri) : this(new Uri(uri)) {
    }

    internal void SetEntries(Uri uri, params TestEntry[] entries) {
        Listings[uri.ToString()] = entries ?? throw new ArgumentNullException(nameof(entries));
    }

    internal IReadOnlyList<TestEntry> EntriesAt(Uri uri) {
        ListedUris.Add(uri);
        return Listings.TryGetValue(uri.ToString(), out var entries)
            ? entries
            : Array.Empty<TestEntry>();
    }

    internal FileProtocolStreamSource<TestEntry> ReadEntry(TestEntry entry) {
        ReadEntries.Add(entry);
        return new TestStreamSource(entry);
    }

    private sealed class ProvideIO : FileProtocolProvider<TestEntry, TestConfig>.ProvideIO<TestProvider> {
        protected sealed override FileProtocolStreamSource<TestEntry> Read(TestProvider provider, TestEntry entry) {
            return provider.ReadEntry(entry);
        }

        protected sealed override async IAsyncEnumerable<TestEntry> List(
            TestProvider provider,
            Uri uri,
            [EnumeratorCancellation] CancellationToken token) {
            foreach (var entry in provider.EntriesAt(uri)) {
                token.ThrowIfCancellationRequested();
                yield return entry;
            }
            await Task.CompletedTask;
        }
    }

    private sealed class CopyIO : CopyProvidedIO<TestProvider> {
        public List<string> Directories { get; } = [];
        public Dictionary<string, MemoryStream> Outputs { get; } = [];
        public int FailWriteAt { get; set; }
        public int Writes { get; private set; }

        protected sealed override Task CreateDirectory(TestProvider provider, string path, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            Directories.Add(path);
            return Task.CompletedTask;
        }

        protected sealed override Task<Stream> Write(TestProvider provider,
                                                     string path,
                                                     long length,
                                                     CancellationToken token) {
            token.ThrowIfCancellationRequested();
            Writes++;
            if (Writes == FailWriteAt) {
                return Task.FromException<Stream>(new IOException("The destination write failed."));
            }
            var output = provider.RemoteFileSystem.OpenWrite(path);
            Outputs.Add(path, output);
            return Task.FromResult<Stream>(output);
        }
    }
}

internal sealed class TestCommandSource : ICommandSource {
    private readonly IReadOnlyList<object> Items;

    public TestCommandSource(params object[] items) {
        Items = items ?? throw new ArgumentNullException(nameof(items));
    }

    object ICommandSource.Item => Items.FirstOrDefault();

    IEnumerable ICommandSource.Items => Items;
}

internal sealed class TestEntry : FileProtocolEntry<TestProvider> {
    public sealed override Uri Uri { get; }
    public sealed override FileProtocolEntryKind Kind { get; }
    public sealed override string Name => NameValue ?? base.Name;

    public string NameValue { get; init; }

    public TestEntry(TestProvider provider, Uri uri, FileProtocolEntryKind kind = FileProtocolEntryKind.File) : base(provider) {
        Uri = uri;
        Kind = kind;
    }

    public TestEntry(TestProvider provider, string uri, FileProtocolEntryKind kind = FileProtocolEntryKind.File)
        : this(provider, new Uri(uri), kind) {
    }
}

internal sealed class TestStreamSource : FileProtocolStreamSource<TestEntry> {
    public sealed override long StreamLength => 0;
    public sealed override string RelativePath => Entry.Name;

    public TestStreamSource(TestEntry entry) : base(entry) {
    }
}

internal sealed class TestPayloadStreamSource : FileProtocolStreamSource<TestEntry> {
    private readonly byte[] Payload;
    private readonly long Length;
    private readonly Exception StreamReadyFailure;

    public sealed override long StreamLength => Length;
    public sealed override string RelativePath { get; }

    protected sealed override Task<IEntryStreamReady> StreamReady(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        if (StreamReadyFailure is not null) {
            return Task.FromException<IEntryStreamReady>(StreamReadyFailure);
        }
        Stream = new MemoryStream(Payload, writable: false);
        return Task.FromResult<IEntryStreamReady>(new EntryStreamReady());
    }

    public TestPayloadStreamSource(TestEntry entry,
                                   string relativePath,
                                   string contents,
                                   bool streamLengthIsKnown = true,
                                   Exception streamReadyFailure = null) : base(entry) {
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        Payload = Encoding.UTF8.GetBytes(contents ?? throw new ArgumentNullException(nameof(contents)));
        Length = streamLengthIsKnown ? Payload.Length : -1;
        StreamReadyFailure = streamReadyFailure;
    }
}

internal sealed class TestEntryStreamSet : IEntryStreamSet {
    private readonly IReadOnlyList<IEntryStreamSource> Sources;

    public TestEntryStreamSet(params IEntryStreamSource[] sources) {
        Sources = sources ?? throw new ArgumentNullException(nameof(sources));
    }

    public IEntryStreamReady StreamSourceReady() {
        return null;
    }

    public IEnumerable<string> FileSource() {
        return Array.Empty<string>();
    }

    public async IAsyncEnumerable<IEntryStreamSource> StreamSource([EnumeratorCancellation] CancellationToken token) {
        foreach (var source in Sources) {
            token.ThrowIfCancellationRequested();
            yield return source;
        }
        await Task.CompletedTask;
    }
}

internal sealed class TestFileSourceSet : IEntryStreamSet {
    private readonly IReadOnlyList<string> Paths;

    public TestFileSourceSet(params string[] paths) {
        Paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public IEntryStreamReady StreamSourceReady() {
        return null;
    }

    public IEnumerable<string> FileSource() {
        return Paths;
    }

    public async IAsyncEnumerable<IEntryStreamSource> StreamSource([EnumeratorCancellation] CancellationToken token) {
        await Task.CompletedTask;
        yield break;
    }
}

internal sealed class TestOperationProgress : IOperationProgress {
    private readonly Lock Locker = new();
    private readonly CancellationTokenSource Cancellation;
    private readonly TestOperationProgress Parent;

    public OperationProgressKind? Kind { get; private set; }
    public long Progress { get; private set; }
    public long Target { get; private set; }

    public TestOperationProgress(CancellationTokenSource cancellation = null) : this(cancellation, null) {
    }

    private TestOperationProgress(CancellationTokenSource cancellation, TestOperationProgress parent) {
        Cancellation = cancellation;
        Parent = parent;
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
            var previousProgress = Progress;
            var previousTarget = Target;
            Progress = (setProgress ?? Progress) + (addProgress ?? 0);
            Target = (setTarget ?? Target) + (addTarget ?? 0);
            Kind = kind ?? Kind;
            progressChange = Progress - previousProgress;
            targetChange = Target - previousTarget;
        }
        if (progressChange != 0 || targetChange != 0) {
            Parent?.Change(addProgress: progressChange, addTarget: targetChange);
        }
        if (addProgress.GetValueOrDefault() > 0) {
            Cancellation?.Cancel();
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
            var childProgress = new TestOperationProgress(Cancellation, this);
            await task(childProgress, cancellationToken);
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

internal sealed class TestProviderFactory : FileProtocolProviderFactory<TestProvider> {
    private readonly TestRemoteFileSystem FileSystem;

    protected sealed override Task<TestProvider> CreateFor(Uri uri, IPanel panel, CancellationToken token) {
        CreateForUri = uri;
        CreateForCount++;
        return NullTask
            ? null
            : Task.FromResult(Created ??= new TestProvider(uri, FileSystem));
    }

    /// <summary>
    /// When true, the <see cref="Uri"/> overload returns a null <see cref="Task"/>
    /// rather than a task with a null result.
    /// </summary>
    public bool NullTask { get; init; }

    public Uri CreateForUri { get; private set; }
    public int CreateForCount { get; private set; }
    public TestProvider Created { get; private set; }

    public TestProviderFactory(string scheme, TestRemoteFileSystem fileSystem = null) : base(scheme) {
        FileSystem = fileSystem ?? new TestRemoteFileSystem();
    }

    /// <summary>
    /// Exposes the protected id-based overload under test.
    /// </summary>
    public Task<TestProvider> CreateForID(string id, CancellationToken token = default) {
        return CreateFor(id, panel: null, token);
    }

    public async Task<TestProvider> CreateInitialized(string id, CancellationToken token = default) {
        var provider = await ((IProviderFactory)this).CreateFor(id, panel: null, token);
        return provider as TestProvider;
    }
}
