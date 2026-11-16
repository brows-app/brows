using Brows;
using Brows.DirectoryListing;
using Brows.Panels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

[TestFixture]
internal sealed class FtpDeleteTest {
    [Test]
    public void Delete_RecursesPostorderAndRefreshesAfterWork() {
        using var provider = new TestProvider(new Uri("ftp://example.test/root/"));
        var tree = Entry(provider, provider.Uri, "tree space 25% \"quoted\"", FileProtocolEntryKind.Directory);
        var childDirectory = Entry(provider, tree.Uri, "nested", FileProtocolEntryKind.Directory);
        var rootFile = Entry(provider, tree.Uri, "root 25% \"quoted\".txt", FileProtocolEntryKind.File);
        var childFile = Entry(provider, childDirectory.Uri, "deep.txt", FileProtocolEntryKind.File);
        provider.Listings[tree.Uri.AbsolutePath] = [rootFile, childDirectory];
        provider.Listings[childDirectory.Uri.AbsolutePath] = [childFile];
        var command = new TestDeleteCommand();

        var worked = Work(command, provider, CancellationToken.None, tree);

        Assert.That(worked, Is.True);
        Assert.That(provider.Deletes.Select(call => (call.Name, call.Directory, call.Path)), Is.EqualTo(new[] {
            ("root 25% \"quoted\".txt", false, "/root/tree space 25% \"quoted\"/root 25% \"quoted\".txt"),
            ("deep.txt", false, "/root/tree space 25% \"quoted\"/nested/deep.txt"),
            ("nested", true, "/root/tree space 25% \"quoted\"/nested"),
            ("tree space 25% \"quoted\"", true, "/root/tree space 25% \"quoted\"")
        }));
        Assert.That(command.RefreshCount, Is.EqualTo(1));
    }

    [Test]
    public void Delete_RemovesEmptyDirectoryAndRefreshes() {
        using var provider = new TestProvider(new Uri("ftp://example.test/root/"));
        var emptyDirectory = Entry(provider, provider.Uri, "empty", FileProtocolEntryKind.Directory);
        var command = new TestDeleteCommand();

        var worked = Work(command, provider, CancellationToken.None, emptyDirectory);

        Assert.That(worked, Is.True);
        Assert.That(provider.ListedPaths, Is.EqualTo(new[] { emptyDirectory.Uri.AbsolutePath }));
        Assert.That(
            provider.Deletes.Select(call => (call.Name, call.Directory)),
            Is.EqualTo(new[] { ("empty", true) }));
        Assert.That(command.RefreshCount, Is.EqualTo(1));
    }

    [Test]
    public void Delete_UnlinksFileAndDirectoryLinksWithoutListingTargets() {
        using var provider = new TestProvider(new Uri("ftp://example.test/root/"));
        var fileLink = Entry(provider, provider.Uri, "file-link", FileProtocolEntryKind.FileLink);
        var directoryLink = Entry(provider, provider.Uri, "directory-link", FileProtocolEntryKind.DirectoryLink);
        var command = new TestDeleteCommand();

        var worked = Work(command, provider, CancellationToken.None, fileLink, directoryLink);

        Assert.That(worked, Is.True);
        Assert.That(provider.ListedPaths, Is.Empty);
        Assert.That(provider.Deletes.Select(call => (call.Name, call.Directory)), Is.EqualTo(new[] {
            ("file-link", false),
            ("directory-link", false)
        }));
        Assert.That(command.RefreshCount, Is.EqualTo(1));
    }

    [Test]
    public void Delete_RefreshesAfterPartialWorkWhenPermissionFails() {
        using var provider = new TestProvider(new Uri("ftp://example.test/root/")) {
            FailureName = "denied.txt"
        };
        var directory = Entry(provider, provider.Uri, "tree", FileProtocolEntryKind.Directory);
        var firstFile = Entry(provider, directory.Uri, "first.txt", FileProtocolEntryKind.File);
        var deniedFile = Entry(provider, directory.Uri, "denied.txt", FileProtocolEntryKind.File);
        provider.Listings[directory.Uri.AbsolutePath] = [firstFile, deniedFile];
        var command = new TestDeleteCommand { RefreshFailure = new IOException("Refresh failed.") };

        var failure = Assert.Throws<UnauthorizedAccessException>(
            () => Work(command, provider, CancellationToken.None, directory));

        Assert.That(failure.Message, Is.EqualTo("The FTP server refused deletion."));
        Assert.That(provider.Deletes.Select(call => call.Name), Is.EqualTo(new[] { "first.txt", "denied.txt" }));
        Assert.That(command.RefreshCount, Is.EqualTo(1));
        Assert.That(command.RefreshWasCanceled, Is.False);
        Assert.That(command.RefreshTokenCanBeCanceled, Is.True);
    }

    [Test]
    public void Delete_HonorsCancellationAndRefreshesAfterPartialWork() {
        using var cancellation = new CancellationTokenSource();
        using var provider = new TestProvider(new Uri("ftp://example.test/root/")) {
            Cancellation = cancellation,
            CancelAfterName = "first.txt"
        };
        var directory = Entry(provider, provider.Uri, "tree", FileProtocolEntryKind.Directory);
        var firstFile = Entry(provider, directory.Uri, "first.txt", FileProtocolEntryKind.File);
        var secondFile = Entry(provider, directory.Uri, "second.txt", FileProtocolEntryKind.File);
        provider.Listings[directory.Uri.AbsolutePath] = [firstFile, secondFile];
        var command = new TestDeleteCommand { RefreshFailure = new IOException("Refresh failed.") };

        Assert.Throws<OperationCanceledException>(() => Work(command, provider, cancellation.Token, directory));

        Assert.That(provider.Deletes.Select(call => call.Name), Is.EqualTo(new[] { "first.txt" }));
        Assert.That(command.RefreshCount, Is.EqualTo(1));
        Assert.That(command.RefreshWasCanceled, Is.False);
        Assert.That(command.RefreshTokenCanBeCanceled, Is.True);
    }

    [Test]
    public void Delete_ReportsRefreshFailureAfterSuccessfulDeletion() {
        using var cancellation = new CancellationTokenSource();
        using var provider = new TestProvider(new Uri("ftp://example.test/root/"));
        var file = Entry(provider, provider.Uri, "file.txt", FileProtocolEntryKind.File);
        var command = new TestDeleteCommand { RefreshFailure = new IOException("Refresh failed.") };

        var failure = Assert.Throws<IOException>(
            () => Work(command, provider, cancellation.Token, file));

        Assert.That(failure.Message, Is.EqualTo("Refresh failed."));
        Assert.That(provider.Deletes.Select(call => call.Name), Is.EqualTo(new[] { "file.txt" }));
        Assert.That(command.RefreshCount, Is.EqualTo(1));
        Assert.That(command.RefreshTokenCanBeCanceled, Is.True);
    }

    [Test]
    public void Delete_RejectsDotEntriesBeforeMutating() {
        using var provider = new TestProvider(new Uri("ftp://example.test/root/"));
        var dotEntry = new FtpEntry(
            provider,
            new DirectoryListItem("..", null, null, null, null, null, DirectoryListItemFlags.Directory),
            provider.Uri);
        var command = new TestDeleteCommand();

        Assert.Throws<InvalidDataException>(() => Work(command, provider, CancellationToken.None, dotEntry));

        Assert.That(provider.Deletes, Is.Empty);
        Assert.That(command.RefreshCount, Is.Zero);
    }

    [Test]
    public void Delete_ReturnsFalseAndDoesNotRefreshWithoutSelectedEntries() {
        using var provider = new TestProvider(new Uri("ftp://example.test/root/"));
        var command = new TestDeleteCommand();

        var worked = Work(command, provider, CancellationToken.None);

        Assert.That(worked, Is.False);
        Assert.That(provider.Deletes, Is.Empty);
        Assert.That(command.RefreshCount, Is.Zero);
    }

    private static FtpEntry Entry(
        TestProvider provider,
        Uri parentUri,
        string name,
        FileProtocolEntryKind kind) {
        var flags = kind switch {
            FileProtocolEntryKind.Directory => DirectoryListItemFlags.Directory,
            FileProtocolEntryKind.DirectoryLink => DirectoryListItemFlags.Directory | DirectoryListItemFlags.Link,
            FileProtocolEntryKind.FileLink => DirectoryListItemFlags.Link,
            _ => (DirectoryListItemFlags)0
        };
        var item = new DirectoryListItem(name, null, null, null, null, null, flags);
        var includeTrailingSlash = kind == FileProtocolEntryKind.Directory;
        var uri = FileProtocolUri.Extend(parentUri, [name], includeTrailingSlash);
        return new FtpEntry(provider, item, uri);
    }

    private static bool Work(
        TestDeleteCommand command,
        TestProvider provider,
        CancellationToken token,
        params FtpEntry[] entries) {
        var source = new TestCommandSource();
        foreach (var entry in entries) {
            source.Items.Add(entry);
        }
        var panels = new TestPanelCollection();
        panels.Panels.Add(new TestPanel { Column = 0, Provider = provider });
        var context = new TestCommandContext {
            Panels = panels,
            Source = source,
            Token = token
        };
        return ((ICommand)command).TriggeredWork(context);
    }

    private sealed class TestProvider : FtpProviderBase {
        public Dictionary<string, List<FtpEntry>> Listings { get; } = [];
        public List<string> ListedPaths { get; } = [];
        public List<(string Name, bool Directory, string Path)> Deletes { get; } = [];
        public string FailureName { get; init; }
        public string CancelAfterName { get; init; }
        public CancellationTokenSource Cancellation { get; init; }

        public TestProvider(Uri uri) : base(new TestFactory(), uri, null) {
        }

        internal override IAsyncEnumerable<FtpEntry> ListEntries(Uri uri, CancellationToken token) {
            ListedPaths.Add(uri.AbsolutePath);
            IEnumerable<FtpEntry> entries = Listings.TryGetValue(uri.AbsolutePath, out var listed)
                ? listed
                : Enumerable.Empty<FtpEntry>();
            return Enumerate(entries, token);
        }

        internal override Task DeleteEntry(FtpEntry entry, bool directory, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            Deletes.Add((entry.Name, directory, entry.RemotePath));
            if (entry.Name == FailureName) {
                throw new UnauthorizedAccessException("The FTP server refused deletion.");
            }
            if (entry.Name == CancelAfterName) {
                Cancellation?.Cancel();
            }
            return Task.CompletedTask;
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
    }

    private sealed class TestFactory : IFtpProviderFactory {
        public FtpClientCache Client { get; } = new();
    }

    private sealed class TestDeleteCommand : FtpDelete<TestProvider> {
        public Exception RefreshFailure { get; init; }

        protected override Task Refresh(TestProvider provider, CancellationToken token) {
            RefreshCount++;
            RefreshWasCanceled = token.IsCancellationRequested;
            RefreshTokenCanBeCanceled = token.CanBeCanceled;
            if (RefreshFailure is not null) {
                throw RefreshFailure;
            }
            return Task.CompletedTask;
        }

        public int RefreshCount { get; private set; }
        public bool RefreshWasCanceled { get; private set; }
        public bool RefreshTokenCanBeCanceled { get; private set; }
    }
}
