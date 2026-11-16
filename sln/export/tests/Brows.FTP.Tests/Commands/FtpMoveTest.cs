using Brows;
using Brows.Commands;
using Brows.Composition;
using Brows.DirectoryListing;
using Brows.Entries;
using Brows.Operations;
using Brows.Panels;
using Brows.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

[TestFixture]
internal sealed class FtpMoveTest {
    [Test]
    public void Move_UsesPassivePanelAndRenamesOnTheSameFtpEndpoint() {
        var factory = new TestProvider.TestFactory();
        using var sourceProvider = new TestProvider(new Uri("ftp://user:pass@example.test/source/"), factory);
        using var targetProvider = new TestProvider(new Uri("ftp://user:pass@example.test/target/"), factory);
        var entry = Entry(sourceProvider, "item.txt", FileProtocolEntryKind.File);
        var provide = new TestProvideIO();
        var copy = new TestCopyIO();
        var command = new TestMoveCommand();
        using var cancellation = new CancellationTokenSource();

        var worked = Work(command, sourceProvider, targetProvider, provide, copy, cancellation.Token, entry);

        Assert.Multiple(() => {
            Assert.That(worked, Is.True);
            Assert.That(command.Renames, Is.EqualTo(new[] { ("/source/item.txt", "/target/item.txt") }));
            Assert.That(provide.Calls, Is.Zero);
            Assert.That(copy.Calls, Is.Zero);
            Assert.That(sourceProvider.Deletes, Is.Empty);
            Assert.That(command.Refreshed, Is.EqualTo(new IProvider[] { sourceProvider, targetProvider }));
            Assert.That(command.RefreshTokens, Is.EqualTo(new[] { cancellation.Token, cancellation.Token }));
        });
    }

    [Test]
    public void Move_CopiesWhenConfiguredAccountsDifferAtTheSameEndpoint() {
        using var sourceProvider = new TestProvider(
            new Uri("ftp://example.test/source/"),
            new TestProvider.TestFactory());
        using var targetProvider = new TestProvider(
            new Uri("ftp://example.test/target/"),
            new TestProvider.TestFactory());
        sourceProvider.SetConfiguredUsername("source-profile");
        targetProvider.SetConfiguredUsername("target-profile");
        var entry = Entry(sourceProvider, "item.txt", FileProtocolEntryKind.File);
        var provide = new TestProvideIO();
        var copy = new TestCopyIO { Result = true };
        var command = new TestMoveCommand();

        var worked = Work(command, sourceProvider, targetProvider, provide, copy, entry);

        Assert.Multiple(() => {
            Assert.That(worked, Is.True);
            Assert.That(command.Renames, Is.Empty);
            Assert.That(provide.Calls, Is.EqualTo(1));
            Assert.That(copy.Calls, Is.EqualTo(1));
            Assert.That(sourceProvider.Deletes, Is.EqualTo(new[] { ("item.txt", false) }));
        });
    }

    [Test]
    public void Move_CopiesAcrossServersBeforeDeletingFtpSource() {
        using var sourceProvider = new TestProvider(new Uri("ftp://source.test/source/"));
        using var targetProvider = new TestProvider(new Uri("ftp://target.test/target/"));
        var entry = Entry(sourceProvider, "item.txt", FileProtocolEntryKind.File);
        var provide = new TestProvideIO();
        var copy = new TestCopyIO { Result = true };
        var command = new TestMoveCommand();

        var worked = Work(command, sourceProvider, targetProvider, provide, copy, entry);

        Assert.Multiple(() => {
            Assert.That(worked, Is.True);
            Assert.That(provide.Calls, Is.EqualTo(1));
            Assert.That(copy.Calls, Is.EqualTo(1));
            Assert.That(copy.SourceCount, Is.EqualTo(1));
            Assert.That(sourceProvider.Deletes, Is.EqualTo(new[] { ("item.txt", false) }));
            Assert.That(command.Refreshed, Is.EqualTo(new IProvider[] { sourceProvider, targetProvider }));
        });
    }

    [Test]
    public void Move_RetainsSourceWhenCopyFails() {
        using var sourceProvider = new TestProvider(new Uri("ftp://source.test/source/"));
        using var targetProvider = new TestProvider(new Uri("ftp://target.test/target/"));
        var entry = Entry(sourceProvider, "item.txt", FileProtocolEntryKind.File);
        var provide = new TestProvideIO();
        var copy = new TestCopyIO { Result = false };
        var command = new TestMoveCommand();

        var worked = Work(command, sourceProvider, targetProvider, provide, copy, entry);

        Assert.Multiple(() => {
            Assert.That(worked, Is.False);
            Assert.That(sourceProvider.Deletes, Is.Empty);
            Assert.That(command.Refreshed, Is.EqualTo(new IProvider[] { targetProvider }));
        });
    }

    [Test]
    public void Move_RetainsSourceWhenCanceledAfterCopy() {
        using var sourceProvider = new TestProvider(new Uri("ftp://source.test/source/"));
        using var targetProvider = new TestProvider(new Uri("ftp://target.test/target/"));
        using var cancellation = new CancellationTokenSource();
        var entry = Entry(sourceProvider, "item.txt", FileProtocolEntryKind.File);
        var provide = new TestProvideIO();
        var copy = new TestCopyIO {
            Result = true,
            AfterCopy = cancellation.Cancel
        };
        var command = new TestMoveCommand();

        Assert.Throws<OperationCanceledException>(
            () => Work(command, sourceProvider, targetProvider, provide, copy, cancellation.Token, entry));

        Assert.That(sourceProvider.Deletes, Is.Empty);
        Assert.That(command.Refreshed, Is.EqualTo(new IProvider[] { targetProvider }));
    }

    [Test]
    public void Move_RetainsSourceWhenRemoteFileMetadataChangesDuringCopy() {
        using var sourceProvider = new TestProvider(new Uri("ftp://source.test/source/"));
        using var targetProvider = new TestProvider(new Uri("ftp://target.test/target/"));
        var originalTime = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
        var changedTime = originalTime.AddMinutes(1);
        var entry = Entry(sourceProvider, "item.txt", FileProtocolEntryKind.File, 10, originalTime);
        sourceProvider.Listings[sourceProvider.Uri.AbsolutePath] = [entry];
        var provide = new TestProvideIO();
        var copy = new TestCopyIO {
            Result = true,
            AfterCopy = () => sourceProvider.Listings[sourceProvider.Uri.AbsolutePath] = [
                Entry(sourceProvider, "item.txt", FileProtocolEntryKind.File, 11, changedTime)
            ]
        };
        var command = new TestMoveCommand();

        var failure = Assert.Throws<IOException>(
            () => Work(command, sourceProvider, targetProvider, provide, copy, entry));

        Assert.Multiple(() => {
            Assert.That(
                failure.Message,
                Is.EqualTo("The FTP source listing changed during the move; the source was retained."));
            Assert.That(sourceProvider.Deletes, Is.Empty);
            Assert.That(command.Refreshed, Is.EqualTo(new IProvider[] { targetProvider }));
        });
    }

    [Test]
    public void Move_RefreshesAfterPartialRenameAndPreservesRenameFailure() {
        var factory = new TestProvider.TestFactory();
        using var sourceProvider = new TestProvider(new Uri("ftp://user:pass@example.test/source/"), factory);
        using var targetProvider = new TestProvider(new Uri("ftp://user:pass@example.test/target/"), factory);
        var first = Entry(sourceProvider, "first.txt", FileProtocolEntryKind.File);
        var second = Entry(sourceProvider, "second.txt", FileProtocolEntryKind.File);
        var provide = new TestProvideIO();
        var copy = new TestCopyIO();
        var command = new TestMoveCommand {
            RenameFailureOnCall = 2,
            RenameFailure = new IOException("RNTO failed.")
        };

        var failure = Assert.Throws<IOException>(
            () => Work(command, sourceProvider, targetProvider, provide, copy, first, second));

        Assert.Multiple(() => {
            Assert.That(failure.Message, Is.EqualTo("RNTO failed."));
            Assert.That(command.Renames, Has.Count.EqualTo(2));
            Assert.That(command.Refreshed, Is.EqualTo(new IProvider[] { sourceProvider, targetProvider }));
            Assert.That(command.RefreshTokens.All(token => token.CanBeCanceled), Is.True);
        });
    }

    [Test]
    public void Move_RefreshesAfterPartialDeleteAndPreservesDeleteFailure() {
        using var sourceProvider = new TestProvider(new Uri("ftp://source.test/source/")) {
            DeleteFailureOnCall = 2,
            DeleteFailure = new IOException("DELE failed.")
        };
        using var targetProvider = new TestProvider(new Uri("ftp://target.test/target/"));
        var directory = Entry(sourceProvider, "tree", FileProtocolEntryKind.Directory);
        var first = Entry(sourceProvider, directory.Uri, "first.txt", FileProtocolEntryKind.File);
        var second = Entry(sourceProvider, directory.Uri, "second.txt", FileProtocolEntryKind.File);
        sourceProvider.Listings[directory.Uri.AbsolutePath] = [first, second];
        var provide = new TestProvideIO();
        var copy = new TestCopyIO { Result = true };
        var command = new TestMoveCommand();

        var failure = Assert.Throws<IOException>(
            () => Work(command, sourceProvider, targetProvider, provide, copy, directory));

        Assert.Multiple(() => {
            Assert.That(failure.Message, Is.EqualTo("DELE failed."));
            Assert.That(sourceProvider.Deletes, Has.Count.EqualTo(2));
            Assert.That(command.Refreshed, Is.EqualTo(new IProvider[] { sourceProvider, targetProvider }));
            Assert.That(command.RefreshTokens.All(token => token.CanBeCanceled), Is.True);
        });
    }

    [Test]
    public void Move_RejectsLinkInSelectedTreeBeforeCopyOrDelete() {
        using var sourceProvider = new TestProvider(new Uri("ftp://source.test/source/"));
        using var targetProvider = new TestProvider(new Uri("ftp://target.test/target/"));
        var directory = Entry(sourceProvider, "tree", FileProtocolEntryKind.Directory);
        var link = Entry(sourceProvider, directory.Uri, "link", FileProtocolEntryKind.FileLink);
        sourceProvider.Listings[directory.Uri.AbsolutePath] = [link];
        var provide = new TestProvideIO();
        var copy = new TestCopyIO { Result = true };
        var command = new TestMoveCommand();

        var failure = Assert.Throws<NotSupportedException>(
            () => Work(command, sourceProvider, targetProvider, provide, copy, directory));

        Assert.That(failure.Message, Is.EqualTo("Moving FileLink entries across providers is not supported."));
        Assert.That(provide.Calls, Is.Zero);
        Assert.That(copy.Calls, Is.Zero);
        Assert.That(sourceProvider.Deletes, Is.Empty);
    }

    [TestCase("..", "/root/item.txt", "ftp://user:pass@example.test/root/current/")]
    [TestCase("../sibling", "/root/sibling/item.txt", "ftp://user:pass@example.test/root/current/")]
    [TestCase("/sibling", "/sibling/item.txt", "ftp://user:pass@example.test/root/current/")]
    [TestCase("../sibling", "//root/sibling/item.txt", "ftp://user:pass@example.test//root/current/")]
    [TestCase(
        "../sibling\\ dir 'quoted'",
        "/root/sibling\\ dir 'quoted'/item.txt",
        "ftp://user:pass@example.test/root/current/")]
    public void Move_ExplicitDestinationResolvesParentAndSiblingPaths(
        string destination,
        string expectedPath,
        string providerUri) {
        using var sourceProvider = new TestProvider(new Uri(providerUri));
        var entry = Entry(sourceProvider, "item.txt", FileProtocolEntryKind.File);
        var command = new TestMoveCommand();

        var worked = WorkExplicit(command, sourceProvider, destination, entry);

        Assert.Multiple(() => {
            Assert.That(worked, Is.True);
            Assert.That(command.Renames, Is.EqualTo(new[] { (entry.RemotePath, expectedPath) }));
        });
    }

    [Test]
    public void Move_RejectsAmbiguousExplicitServerAbsoluteDestination() {
        using var sourceProvider = new TestProvider(new Uri("ftp://user:pass@example.test/root/current/"));
        var entry = Entry(sourceProvider, "item.txt", FileProtocolEntryKind.File);
        var command = new TestMoveCommand();

        Assert.Throws<NotSupportedException>(() => WorkExplicit(command, sourceProvider, "//sibling", entry));

        Assert.That(command.Renames, Is.Empty);
    }

    [Test]
    public void Move_RejectsDirectoryDestinationInsideSelectedDirectory() {
        using var sourceProvider = new TestProvider(new Uri("ftp://example.test/root/"));
        var directory = Entry(sourceProvider, "tree", FileProtocolEntryKind.Directory);
        var command = new TestMoveCommand();

        Assert.Throws<IOException>(() => WorkExplicit(command, sourceProvider, "tree/nested", directory));

        Assert.That(command.Renames, Is.Empty);
    }

    [Test]
    public void MoveProvidedIO_DeletesLocalSourceOnlyAfterSuccessfulCopy() {
        var source = Path.Combine(Path.GetTempPath(), "brows-ftp-move-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(source, "move fixture");
        using var provider = new TestProvider(new Uri("ftp://target.test/destination/"));
        var copy = new TestCopyIO { Result = true };
        var move = provider.CreateMoveIO(copy);
        var io = new IProvidedIO[] { new ProvidedIO { StreamSets = [new TestFileStreamSet(source)] } };

        var worked = ((IWorkProvidedIO)move).Work(io, provider, null, CancellationToken.None).GetAwaiter().GetResult();

        Assert.Multiple(() => {
            Assert.That(worked, Is.True);
            Assert.That(File.Exists(source), Is.False);
            Assert.That(copy.Calls, Is.EqualTo(1));
        });
    }

    [Test]
    public void MoveProvidedIO_RetainsLocalSourceWhenCopyFails() {
        var source = Path.Combine(Path.GetTempPath(), "brows-ftp-move-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(source, "move fixture");
        using var provider = new TestProvider(new Uri("ftp://target.test/destination/"));
        var copy = new TestCopyIO { Result = false };
        var move = provider.CreateMoveIO(copy);
        var io = new IProvidedIO[] { new ProvidedIO { StreamSets = [new TestFileStreamSet(source)] } };

        var worked = ((IWorkProvidedIO)move).Work(io, provider, null, CancellationToken.None).GetAwaiter().GetResult();

        Assert.Multiple(() => {
            Assert.That(worked, Is.False);
            Assert.That(File.Exists(source), Is.True);
            Assert.That(copy.Calls, Is.EqualTo(1));
        });
        File.Delete(source);
    }

    [Test]
    public void MoveProvidedIO_RetainsLocalSourceWhenCopyCloseFails() {
        var source = Path.Combine(Path.GetTempPath(), "brows-ftp-move-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(source, "move fixture");
        using var provider = new TestProvider(new Uri("ftp://target.test/destination/"));
        var copy = new TestCopyIO {
            Result = true,
            AfterCopy = () => throw new IOException("FTP CLOSE failed.")
        };
        var move = provider.CreateMoveIO(copy);
        var io = new IProvidedIO[] { new ProvidedIO { StreamSets = [new TestFileStreamSet(source)] } };

        var failure = Assert.Throws<IOException>(
            () => ((IWorkProvidedIO)move).Work(io, provider, null, CancellationToken.None).GetAwaiter().GetResult());

        Assert.Multiple(() => {
            Assert.That(failure.Message, Is.EqualTo("FTP CLOSE failed."));
            Assert.That(File.Exists(source), Is.True);
        });
        File.Delete(source);
    }

    [Test]
    public void MoveProvidedIO_RetainsLocalSourceWhenCanceledAfterCopy() {
        var source = Path.Combine(Path.GetTempPath(), "brows-ftp-move-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(source, "move fixture");
        using var provider = new TestProvider(new Uri("ftp://target.test/destination/"));
        using var cancellation = new CancellationTokenSource();
        var copy = new TestCopyIO {
            Result = true,
            AfterCopy = cancellation.Cancel
        };
        var move = provider.CreateMoveIO(copy);
        var io = new IProvidedIO[] { new ProvidedIO { StreamSets = [new TestFileStreamSet(source)] } };

        Assert.Throws<OperationCanceledException>(
            () => ((IWorkProvidedIO)move).Work(io, provider, null, cancellation.Token).GetAwaiter().GetResult());

        Assert.That(File.Exists(source), Is.True);
        File.Delete(source);
    }

    [Test]
    public void MoveProvidedIO_RetainsNewLocalChildrenThatWereNotInTheCompletedCopy() {
        var source = Path.Combine(Path.GetTempPath(), "brows-ftp-move-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(source);
        var existingChild = Path.Combine(source, "existing.txt");
        var newChild = Path.Combine(source, "new.txt");
        File.WriteAllText(existingChild, "move fixture");
        using var provider = new TestProvider(new Uri("ftp://target.test/destination/"));
        var copy = new TestCopyIO {
            Result = true,
            AfterCopy = () => File.WriteAllText(newChild, "not copied")
        };
        var move = provider.CreateMoveIO(copy);
        var io = new IProvidedIO[] { new ProvidedIO { StreamSets = [new TestFileStreamSet(source)] } };

        var failure = Assert.Throws<IOException>(
            () => ((IWorkProvidedIO)move).Work(io, provider, null, CancellationToken.None).GetAwaiter().GetResult());

        Assert.Multiple(() => {
            Assert.That(
                failure.Message,
                Is.EqualTo("The local source changed during the move; the source was retained."));
            Assert.That(File.Exists(existingChild), Is.True);
            Assert.That(File.Exists(newChild), Is.True);
            Assert.That(Directory.Exists(source), Is.True);
        });
        File.Delete(existingChild);
        File.Delete(newChild);
        Directory.Delete(source);
    }

    private static FtpEntry Entry(
        TestProvider provider,
        string name,
        FileProtocolEntryKind kind,
        long? size = null,
        DateTime? time = null) {
        return Entry(provider, provider.Uri, name, kind, size, time);
    }

    private static FtpEntry Entry(
        TestProvider provider,
        Uri parentUri,
        string name,
        FileProtocolEntryKind kind,
        long? size = null,
        DateTime? time = null) {
        var flags = kind switch {
            FileProtocolEntryKind.Directory => DirectoryListItemFlags.Directory,
            FileProtocolEntryKind.DirectoryLink => DirectoryListItemFlags.Directory | DirectoryListItemFlags.Link,
            FileProtocolEntryKind.FileLink => DirectoryListItemFlags.Link,
            _ => (DirectoryListItemFlags)0
        };
        var item = new DirectoryListItem(name, size, null, null, null, time, flags);
        var uri = FileProtocolUri.Extend(
            parentUri,
            [name],
            includeTrailingSlash: kind == FileProtocolEntryKind.Directory);
        var entry = new FtpEntry(provider, item, uri);
        provider.AddListing(parentUri, entry);
        return entry;
    }

    private static bool Work(
        TestMoveCommand command,
        TestProvider sourceProvider,
        TestProvider targetProvider,
        TestProvideIO provide,
        TestCopyIO copy,
        params FtpEntry[] entries) {
        return Work(command, sourceProvider, targetProvider, provide, copy, CancellationToken.None, entries);
    }

    private static bool Work(
        TestMoveCommand command,
        TestProvider sourceProvider,
        TestProvider targetProvider,
        TestProvideIO provide,
        TestCopyIO copy,
        CancellationToken token,
        params FtpEntry[] entries) {
        var active = new TestPanel { Column = 0, Provider = sourceProvider };
        active.Services.Add(provide);
        var passive = new TestPanel { Column = 1, Provider = targetProvider };
        passive.Services.Add(copy);
        var panels = new TestPanelCollection();
        panels.Panels.Add(active);
        panels.Panels.Add(passive);
        return ((ICommand)command).TriggeredWork(new TestCommandContext {
            Panels = panels,
            Source = Source(entries),
            Token = token
        });
    }

    private static bool WorkExplicit(
        TestMoveCommand command,
        TestProvider provider,
        string destination,
        params FtpEntry[] entries) {
        var panel = new TestPanel { Column = 0, Provider = provider };
        var panels = new TestPanelCollection();
        panels.Panels.Add(panel);
        return ((ICommand)command).TriggeredWork(new TestCommandContext {
            Panels = panels,
            Source = Source(entries),
            Line = new TestCommandLine { Parameter = destination }
        });
    }

    private static TestCommandSource Source(params FtpEntry[] entries) {
        var source = new TestCommandSource();
        foreach (var entry in entries) {
            source.Items.Add(entry);
        }
        return source;
    }

    private sealed class TestProvider : FtpProviderBase {
        public Dictionary<string, List<FtpEntry>> Listings { get; } = [];
        public List<(string Name, bool Directory)> Deletes { get; } = [];
        public int DeleteFailureOnCall { get; init; }
        public Exception DeleteFailure { get; init; }

        public TestProvider(Uri uri) : this(uri, new TestFactory()) {
        }

        internal TestProvider(Uri uri, TestFactory factory) : base(factory, uri, null) {
        }

        public void SetConfiguredUsername(string username) {
            var client = ClientAt(Uri);
            client.Credential.Username = username;
        }

        public void AddListing(Uri parentUri, FtpEntry entry) {
            if (!Listings.TryGetValue(parentUri.AbsolutePath, out var entries)) {
                entries = [];
                Listings[parentUri.AbsolutePath] = entries;
            }
            entries.Add(entry);
        }

        internal override IAsyncEnumerable<FtpEntry> ListEntries(Uri uri, CancellationToken token) {
            var entries = Listings.TryGetValue(uri.AbsolutePath, out var listed)
                ? listed
                : Enumerable.Empty<FtpEntry>();
            return Enumerate(entries, token);
        }

        internal override Task DeleteEntry(FtpEntry entry, bool directory, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            Deletes.Add((entry.Name, directory));
            var shouldFail = DeleteFailure is not null && Deletes.Count == DeleteFailureOnCall;
            if (shouldFail) {
                return Task.FromException(DeleteFailure);
            }
            return Task.CompletedTask;
        }

        internal IWorkProvidedIO CreateMoveIO(IWorkProvidedIO copy) {
            return new TestMoveProvidedIO(copy);
        }

        private static async IAsyncEnumerable<FtpEntry> Enumerate(
            IEnumerable<FtpEntry> entries,
            [EnumeratorCancellation] CancellationToken token) {
            foreach (var entry in entries) {
                token.ThrowIfCancellationRequested();
                yield return entry;
                await Task.CompletedTask;
            }
        }

        internal sealed class TestFactory : IFtpProviderFactory {
            public FtpClientCache Client { get; } = new();
        }

        private sealed class TestMoveProvidedIO : FtpMoveProvidedIO<TestProvider> {
            private readonly IWorkProvidedIO CopyService;

            internal TestMoveProvidedIO(IWorkProvidedIO copy) {
                CopyService = copy ?? throw new ArgumentNullException(nameof(copy));
            }

            protected sealed override IWorkProvidedIO Copy => CopyService;
        }
    }

    private sealed class TestMoveCommand : FtpMove<TestProvider> {
        public int RenameFailureOnCall { get; init; }
        public Exception RenameFailure { get; init; }

        protected override Task Rename(
            TestProvider provider,
            FtpEntry entry,
            string destinationPath,
            CancellationToken token) {
            Renames.Add((entry.RemotePath, destinationPath));
            var shouldFail = RenameFailure is not null && Renames.Count == RenameFailureOnCall;
            if (shouldFail) {
                return Task.FromException(RenameFailure);
            }
            return Task.CompletedTask;
        }

        protected override Task RefreshProvider(IProvider provider, CancellationToken token) {
            Refreshed.Add(provider);
            RefreshTokens.Add(token);
            return Task.CompletedTask;
        }

        protected override FtpMove<TestProvider>.Parameter ParameterFactory(string parameter, string conf) {
            return new() { Destination = parameter };
        }

        public List<(string Source, string Destination)> Renames { get; } = [];
        public List<IProvider> Refreshed { get; } = [];
        public List<CancellationToken> RefreshTokens { get; } = [];
    }

    private sealed class TestProvideIO : IProvideIO {
        public int Calls { get; private set; }

        public Task<bool> Work(
            ICollection<IProvidedIO> io,
            ICommandSource source,
            IProvider target,
            IOperationProgress progress,
            CancellationToken token) {
            Calls++;
            io.Add(new ProvidedIO { StreamSets = [] });
            return Task.FromResult(true);
        }
    }

    private sealed class TestCopyIO : ICopyProvidedIO, IWorkProvidedIO {
        public bool Result { get; init; }
        public Action AfterCopy { get; init; }
        public int Calls { get; private set; }
        public int SourceCount { get; private set; }

        public Task<bool> Work(
            IEnumerable<IProvidedIO> io,
            IProvider target,
            IOperationProgress progress,
            CancellationToken token) {
            var providedIO = io.ToList();
            Calls++;
            SourceCount = providedIO.Count;
            AfterCopy?.Invoke();
            return Task.FromResult(Result);
        }
    }

    private sealed class TestFileStreamSet : IEntryStreamSet {
        private readonly string Source;

        public TestFileStreamSet(string source) {
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public IEntryStreamReady StreamSourceReady() {
            return null;
        }

        public IEnumerable<string> FileSource() {
            return [Source];
        }

        public IAsyncEnumerable<IEntryStreamSource> StreamSource(CancellationToken token) {
            return null;
        }
    }
}
