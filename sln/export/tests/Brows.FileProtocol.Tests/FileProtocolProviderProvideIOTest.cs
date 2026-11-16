using Brows.Composition;
using Brows.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
public sealed class FileProtocolProviderProvideIOTest {
    [Test]
    public async Task Work_RejectsLinksInAMixedTreeBeforeCopyingAnyEntry() {
        using var provider = new TestProvider("ftp://host/root/");
        using var target = new TestProvider("ftp://host/target/");
        var directory = new TestEntry(
            provider,
            "ftp://host/root/folder/",
            FileProtocolEntryKind.Directory);
        var directFile = new TestEntry(provider, "ftp://host/root/folder/direct.txt");
        var nestedDirectory = new TestEntry(
            provider,
            "ftp://host/root/folder/nested/",
            FileProtocolEntryKind.Directory);
        var directoryLink = new TestEntry(
            provider,
            "ftp://host/root/folder/nested/linked-directory/",
            FileProtocolEntryKind.DirectoryLink);
        var fileLink = new TestEntry(
            provider,
            "ftp://host/root/folder/nested/linked.txt",
            FileProtocolEntryKind.FileLink);
        var nestedFile = new TestEntry(provider, "ftp://host/root/folder/nested/deep.txt");
        provider.SetEntries(directory.Uri, nestedDirectory, directFile);
        provider.SetEntries(nestedDirectory.Uri, nestedFile, fileLink, directoryLink);
        provider.SetEntries(
            directoryLink.Uri,
            new TestEntry(provider, "ftp://host/root/folder/nested/linked-directory/child.txt"));

        var result = new List<IProvidedIO>();
        var source = new TestCommandSource(directory);

        var worked = await provider.ProvideIOService.Work(
            result,
            source,
            provider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.True);
        await Assert.ThrowsAsync<NotSupportedException>(async () => await target.CopyProvidedIO.Work(
            result,
            target,
            progress: null,
            CancellationToken.None));
        Assert.That(provider.ListRequests, Is.EqualTo(new[] { directory.Uri, nestedDirectory.Uri }));
        Assert.That(provider.EntriesRead, Is.EqualTo(new[] { directFile, nestedFile }));
        Assert.That(target.CopyWrites, Is.Zero);
        Assert.That(target.CopyDirectories, Is.Empty);
    }

    [Test]
    public async Task Work_RejectsLinkOnlySelectionInsteadOfProvidingAnEmptySuccessfulTransfer() {
        using var provider = new TestProvider("ftp://host/root/");
        using var target = new TestProvider("ftp://host/target/");
        var fileLink = new TestEntry(
            provider,
            "ftp://host/root/linked.txt",
            FileProtocolEntryKind.FileLink);
        var result = new List<IProvidedIO>();

        var worked = await provider.ProvideIOService.Work(
            result,
            new TestCommandSource(fileLink),
            provider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.True);
        await Assert.ThrowsAsync<NotSupportedException>(async () => await target.CopyProvidedIO.Work(
            result,
            target,
            progress: null,
            CancellationToken.None));
        Assert.That(target.CopyWrites, Is.Zero);
        Assert.That(target.CopyDirectories, Is.Empty);
    }

    [Test]
    public async Task Work_RejectsDirectoryLinkWithoutRecursingIntoItsTarget() {
        using var provider = new TestProvider("ftp://host/root/");
        using var target = new TestProvider("ftp://host/target/");
        var directoryLink = new TestEntry(
            provider,
            "ftp://host/root/linked-directory/",
            FileProtocolEntryKind.DirectoryLink);
        provider.SetEntries(
            directoryLink.Uri,
            new TestEntry(provider, "ftp://host/root/linked-directory/child.txt"));
        var result = new List<IProvidedIO>();

        var worked = await provider.ProvideIOService.Work(
            result,
            new TestCommandSource(directoryLink),
            provider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.True);
        await Assert.ThrowsAsync<NotSupportedException>(async () => await target.CopyProvidedIO.Work(
            result,
            target,
            progress: null,
            CancellationToken.None));
        Assert.That(provider.ListRequests, Is.Empty);
        Assert.That(provider.EntriesRead, Is.Empty);
        Assert.That(target.CopyWrites, Is.Zero);
        Assert.That(target.CopyDirectories, Is.Empty);
    }

    [Test]
    public async Task Work_ProvidesAndCopiesSelectedAndNestedEmptyDirectories() {
        var sourceFactory = new TestProviderFactory("ftp");
        var targetFactory = new TestProviderFactory("ftp");
        using var sourceProvider = await sourceFactory.CreateInitialized("ftp://host/source/");
        using var targetProvider = await targetFactory.CreateInitialized("ftp://host/target/");
        var selectedDirectory = new TestEntry(
            sourceProvider,
            "ftp://host/source/one/two/selected/",
            FileProtocolEntryKind.Directory);
        var nestedDirectory = new TestEntry(
            sourceProvider,
            "ftp://host/source/one/two/selected/nested/",
            FileProtocolEntryKind.Directory);
        sourceProvider.SetEntries(selectedDirectory.Uri, nestedDirectory);

        var result = new List<IProvidedIO>();
        var worked = await sourceProvider.ProvideIOService.Work(
            result,
            new TestCommandSource(selectedDirectory),
            sourceProvider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.True);
        var streamSet = result.Single().StreamSets.Single();
        var directorySources = new List<IEntryStreamSource>();
        await foreach (var source in streamSet.StreamSource(CancellationToken.None)) {
            directorySources.Add(source);
        }
        Assert.That(directorySources.Select(source => source.EntryID), Is.EqualTo(new[] {
            selectedDirectory.ID,
            nestedDirectory.ID
        }));
        Assert.That(directorySources.Select(source => source.RelativePath), Is.EqualTo(new[] {
            "one/two/selected/",
            "one/two/selected/nested/"
        }));
        Assert.That(directorySources.All(source => source.Stream is null), Is.True);
        Assert.That(directorySources.All(source => source.SourceDirectory is not null), Is.True);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            targetProvider.CopyProvidedIO.Work(result, targetProvider, progress: null, cancellation.Token));
        Assert.That(targetProvider.CopyDirectories, Is.Empty);

        var copied = await targetProvider.CopyProvidedIO.Work(
            result,
            targetProvider,
            progress: null,
            CancellationToken.None);

        Assert.That(copied, Is.True);
        Assert.That(targetProvider.CopyDirectories, Is.EqualTo(new[] {
            "/target/one",
            "/target/one/two",
            "/target/one/two/selected",
            "/target/one/two/selected/nested"
        }));
        Assert.That(targetProvider.CopyWrites, Is.Zero);
        Assert.That(targetProvider.Listed, Is.GreaterThan(0));
    }

    [Test]
    public async Task Work_ProvidesStreamSetWithTheDefaultStreamSourceConcurrency() {
        using var provider = new TestProvider("ftp://host/root/");
        var file = new TestEntry(provider, "ftp://host/root/file.txt");
        var result = new List<IProvidedIO>();

        var worked = await provider.ProvideIOService.Work(
            result,
            new TestCommandSource(file),
            provider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.True);
        var streamSet = result.Single().StreamSets.Single();
        Assert.That(streamSet.StreamSourceConcurrency, Is.EqualTo(new TestConfig().StreamSourceConcurrency));
    }
}
