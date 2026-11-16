using Brows.Composition;
using Brows.Operations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
public sealed class FileProtocolProviderCopyProvidedIOTest {
    [Test]
    public async Task Work_ReturnsFalseForEmptyStreamSetsWithoutWriting() {
        var factory = new Brows.Tests.TestProviderFactory("ftp");
        using var provider = await factory.CreateInitialized("ftp://host/target/");
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet()]
        };

        var worked = await provider.CopyProvidedIO.Work(
            [io],
            provider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.False);
        Assert.That(provider.CopyWrites, Is.Zero);
        Assert.That(provider.CopyDirectories, Is.Empty);
    }

    [Test]
    public async Task Work_PreservesLiteralRemoteFileAndDirectoryNames() {
        var fileSystem = new Brows.Tests.TestRemoteFileSystem();
        using var provider = new Brows.Tests.TestProvider(new Uri("sftp://user@host/target/"), fileSystem);
        var sourceRoot = new Uri("sftp://user@host/source/");
        var relativePaths = new[] {
            " leading.txt",
            "trailing.txt ",
            "whitespace/   ",
            " collision.txt",
            "collision.txt "
        };
        var sources = new List<IEntryStreamSource>();
        var sourceUris = new List<Uri>();
        for (var index = 0; index < relativePaths.Length; index++) {
            var sourceUri = FileProtocolUri.Extend(sourceRoot, [$"source-{index}.txt"], includeTrailingSlash: false);
            fileSystem.SetFile(sourceUri, $"contents-{index}");
            sourceUris.Add(sourceUri);
            sources.Add(new Brows.Tests.TestRemoteStreamSource(
                fileSystem,
                sourceUri.ToString(),
                Uri.UnescapeDataString(sourceUri.AbsolutePath),
                relativePaths[index]));
        }

        var directoryUri = FileProtocolUri.Extend(sourceRoot, ["source-directory"], includeTrailingSlash: true);
        var childUri = FileProtocolUri.Extend(directoryUri, ["child.txt"], includeTrailingSlash: false);
        fileSystem.SetFile(childUri, "directory child");
        var directory = new Brows.Tests.TestRemoteStreamSource(
            fileSystem,
            directoryUri.ToString(),
            Uri.UnescapeDataString(directoryUri.AbsolutePath),
            "   /",
            "   /");
        var child = new Brows.Tests.TestRemoteStreamSource(
            fileSystem,
            childUri.ToString(),
            Uri.UnescapeDataString(childUri.AbsolutePath),
            "   /child.txt");
        var existingDestination = FileProtocolUri.Extend(provider.Uri, ["collision.txt"], includeTrailingSlash: false);
        fileSystem.SetFile(existingDestination, "existing destination");
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet([.. sources, directory, child])]
        };

        var worked = await provider.CopyProvidedIO.Work(
            [io],
            provider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.True);
        Assert.That(provider.CopyOutputs.Keys, Is.EquivalentTo(new[] {
            "/target/ leading.txt",
            "/target/trailing.txt ",
            "/target/whitespace/   ",
            "/target/ collision.txt",
            "/target/collision.txt ",
            "/target/   /child.txt"
        }));
        Assert.That(provider.CopyDirectories, Is.EqualTo(new[] {
            "/target/whitespace",
            "/target/   "
        }));
        for (var index = 0; index < relativePaths.Length; index++) {
            var destination = FileProtocolUri.CombinePath(provider.Uri, relativePaths[index]);
            Assert.That(
                Encoding.UTF8.GetString(provider.RemoteFileSystem.Read(destination)),
                Is.EqualTo($"contents-{index}"));
            Assert.That(fileSystem.FileContents(sourceUris[index]), Is.EqualTo($"contents-{index}"));
        }
        Assert.That(
            Encoding.UTF8.GetString(provider.RemoteFileSystem.Read("/target/   /child.txt")),
            Is.EqualTo("directory child"));
        Assert.That(fileSystem.FileContents(existingDestination), Is.EqualTo("existing destination"));
    }

    [Test]
    public async Task Work_PreservesLeadingSpaceInLocalProvidedFileName() {
        var factory = new Brows.Tests.TestProviderFactory("ftp");
        using var provider = await factory.CreateInitialized("ftp://host/target/");
        var sourceRoot = Path.Combine(Path.GetTempPath(), "brows-fileprotocol-" + Guid.NewGuid().ToString("N"));
        var localFile = Path.Combine(sourceRoot, " leading.txt");
        try {
            Directory.CreateDirectory(sourceRoot);
            File.WriteAllText(localFile, "local file contents");
            var io = new ProvidedIO {
                StreamSets = [new Brows.Tests.TestFileSourceSet(localFile)]
            };

            var worked = await provider.CopyProvidedIO.Work(
                [io],
                provider,
                progress: null,
                CancellationToken.None);

            Assert.That(worked, Is.True);
            Assert.That(provider.CopyOutputs.Keys, Is.EqualTo(new[] {
                "/target/ leading.txt"
            }));
            Assert.That(
                Encoding.UTF8.GetString(provider.CopyOutputs["/target/ leading.txt"].ToArray()),
                Is.EqualTo("local file contents"));
        }
        finally {
            var directoryPath = Path.GetFullPath(sourceRoot);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var directoryIsWithinTemporaryRoot = directoryPath.StartsWith(
                temporaryRoot,
                StringComparison.OrdinalIgnoreCase);
            if (directoryIsWithinTemporaryRoot && Directory.Exists(directoryPath)) {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
    }

    [Test]
    public async Task Work_CreatesParentDirectoriesCopiesStreamsTracksProgressAndRefreshes() {
        var factory = new Brows.Tests.TestProviderFactory("ftp");
        using var provider = await factory.CreateInitialized("ftp://host/target/");
        var copy = provider.CopyProvidedIO;
        var progress = new Brows.Tests.TestOperationProgress();
        var entry = new Brows.Tests.TestEntry(provider, "ftp://host/source/file.txt") {
            NameValue = "file.txt"
        };
        var source = new Brows.Tests.TestPayloadStreamSource(entry, "one/two/file.txt", "protocol data");
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet(source)]
        };

        var worked = await copy.Work([io], provider, progress, CancellationToken.None);

        Assert.That(worked, Is.True);
        Assert.That(provider.CopyDirectories, Is.EqualTo(new[] {
            "/target/one",
            "/target/one/two"
        }));
        Assert.That(provider.CopyOutputs.Keys, Is.EquivalentTo(new[] {
            "/target/one/two/file.txt"
        }));
        Assert.That(
            Encoding.UTF8.GetString(provider.CopyOutputs.Values.Single().ToArray()),
            Is.EqualTo("protocol data"));
        Assert.That(progress.Progress, Is.EqualTo(Encoding.UTF8.GetByteCount("protocol data")));
        Assert.That(progress.Target, Is.EqualTo(Encoding.UTF8.GetByteCount("protocol data")));
        Assert.That(provider.Listed, Is.GreaterThan(0));
    }

    [Test]
    public async Task Work_ExpandsTheTargetForAnUnknownLengthStream() {
        var factory = new Brows.Tests.TestProviderFactory("ftp");
        using var provider = await factory.CreateInitialized("ftp://host/target/");
        var progress = new Brows.Tests.TestOperationProgress();
        var entry = new Brows.Tests.TestEntry(provider, "ftp://host/source/file.txt") {
            NameValue = "file.txt"
        };
        var source = new Brows.Tests.TestPayloadStreamSource(
            entry,
            "file.txt",
            "unknown stream length",
            streamLengthIsKnown: false);
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet(source)]
        };

        var worked = await provider.CopyProvidedIO.Work([io], provider, progress, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(worked, Is.True);
            Assert.That(progress.Kind, Is.EqualTo(OperationProgressKind.FileSize));
            Assert.That(progress.Progress, Is.EqualTo(Encoding.UTF8.GetByteCount("unknown stream length")));
            Assert.That(progress.Target, Is.EqualTo(progress.Progress));
        }
    }

    [Test]
    public async Task Work_RejectsSameFileAcrossEquivalentUrisAndProvidersBeforeWriting() {
        var fileSystem = new Brows.Tests.TestRemoteFileSystem();
        using var sourceProvider = new Brows.Tests.TestProvider(new Uri("ftp://Alice:source@HOST/root/"), fileSystem);
        var sourceUri = FileProtocolUri.Extend(sourceProvider.Uri, ["file name.txt"], includeTrailingSlash: false);
        var targetFactory = new Brows.Tests.TestProviderFactory("ftp", fileSystem);
        using var targetProvider = await targetFactory.CreateInitialized("FTP://Alice:target@host:21/root/");
        fileSystem.SetFile(sourceUri, "source bytes must survive");
        var source = new Brows.Tests.TestRemoteStreamSource(
            fileSystem,
            sourceUri.ToString(),
            Uri.UnescapeDataString(sourceUri.AbsolutePath),
            "file name.txt");
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet(source)]
        };

        var worked = await targetProvider.CopyProvidedIO.Work(
            [io],
            targetProvider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.False);
        Assert.That(targetProvider.CopyWrites, Is.Zero);
        Assert.That(targetProvider.CopyDirectories, Is.Empty);
        Assert.That(source.OpenCount, Is.Zero);
        Assert.That(fileSystem.FileContents(sourceUri), Is.EqualTo("source bytes must survive"));
    }

    [Test]
    public async Task Work_RejectsSameDirectoryAcrossEquivalentSftpPanesBeforeCreatingOutput() {
        var fileSystem = new Brows.Tests.TestRemoteFileSystem();
        using var sourceProvider = new Brows.Tests.TestProvider(new Uri("sftp://host/root/tree/"), fileSystem);
        var sourceUri = sourceProvider.Uri;
        using var targetProvider = new Brows.Tests.TestProvider(
            new Uri($"sftp://{Uri.EscapeDataString(Environment.UserName)}@HOST:22/root/tree/"),
            fileSystem);
        var childUri = FileProtocolUri.Extend(sourceUri, ["keep.txt"], includeTrailingSlash: false);
        fileSystem.SetFile(childUri, "directory bytes must survive");
        var directory = new Brows.Tests.TestRemoteStreamSource(
            fileSystem,
            sourceUri.ToString(),
            Uri.UnescapeDataString(sourceUri.AbsolutePath),
            "tree/",
            "tree/");
        var file = new Brows.Tests.TestRemoteStreamSource(
            fileSystem,
            childUri.ToString(),
            Uri.UnescapeDataString(childUri.AbsolutePath),
            "tree/keep.txt");
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet(directory, file)]
        };

        var worked = await targetProvider.CopyProvidedIO.Work(
            [io],
            targetProvider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.False);
        Assert.That(targetProvider.CopyWrites, Is.Zero);
        Assert.That(targetProvider.CopyDirectories, Is.Empty);
        Assert.That(directory.OpenCount, Is.Zero);
        Assert.That(file.OpenCount, Is.Zero);
        Assert.That(fileSystem.FileContents(childUri), Is.EqualTo("directory bytes must survive"));
    }

    [Test]
    public async Task Work_RejectsDirectoryDescendantBeforeProcessingAnyStreamSet() {
        var fileSystem = new Brows.Tests.TestRemoteFileSystem();
        var targetUri = new Uri("sftp://user@host/root/tree/nested/");
        var sourceFileUri = new Uri("sftp://user@host/other/first.txt");
        using var sourceProvider = new Brows.Tests.TestProvider(new Uri("sftp://user@host/root/"), fileSystem);
        var sourceDirectoryUri = FileProtocolUri.Extend(sourceProvider.Uri, ["tree"], includeTrailingSlash: true);
        var nestedFileUri = FileProtocolUri.Extend(sourceDirectoryUri, ["child.txt"], includeTrailingSlash: false);
        using var targetProvider = new Brows.Tests.TestProvider(targetUri, fileSystem);
        fileSystem.SetFile(sourceFileUri, "first source bytes must survive");
        fileSystem.SetFile(nestedFileUri, "nested source bytes must survive");
        var firstSource = new Brows.Tests.TestRemoteStreamSource(
            fileSystem,
            sourceFileUri.ToString(),
            Uri.UnescapeDataString(sourceFileUri.AbsolutePath),
            "first.txt");
        var directorySource = new Brows.Tests.TestRemoteStreamSource(
            fileSystem,
            sourceDirectoryUri.ToString(),
            Uri.UnescapeDataString(sourceDirectoryUri.AbsolutePath),
            "tree/",
            "tree/");
        var nestedFileSource = new Brows.Tests.TestRemoteStreamSource(
            fileSystem,
            nestedFileUri.ToString(),
            Uri.UnescapeDataString(nestedFileUri.AbsolutePath),
            "tree/child.txt");
        var io = new ProvidedIO {
            StreamSets = [
                new Brows.Tests.TestEntryStreamSet(firstSource),
                new Brows.Tests.TestEntryStreamSet(directorySource, nestedFileSource)
            ]
        };

        var worked = await targetProvider.CopyProvidedIO.Work(
            [io],
            targetProvider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.False);
        Assert.That(targetProvider.CopyWrites, Is.Zero);
        Assert.That(targetProvider.CopyDirectories, Is.Empty);
        Assert.That(firstSource.OpenCount, Is.Zero);
        Assert.That(directorySource.OpenCount, Is.Zero);
        Assert.That(nestedFileSource.OpenCount, Is.Zero);
        Assert.That(fileSystem.FileContents(sourceFileUri), Is.EqualTo("first source bytes must survive"));
        Assert.That(fileSystem.FileContents(nestedFileUri), Is.EqualTo("nested source bytes must survive"));
    }

    [Test]
    public async Task Work_PreflightsRemoteSourcesBeforeWritingMixedLocalAndRemoteSets() {
        var fileSystem = new Brows.Tests.TestRemoteFileSystem();
        var targetFactory = new Brows.Tests.TestProviderFactory("ftp", fileSystem);
        using var targetProvider = await targetFactory.CreateInitialized("ftp://user@host/root/");
        var remoteSourceUri = new Uri("ftp://user@host/root/destination.txt");
        fileSystem.SetFile(remoteSourceUri, "remote source bytes must survive");
        var remoteSource = new Brows.Tests.TestRemoteStreamSource(
            fileSystem,
            remoteSourceUri.ToString(),
            Uri.UnescapeDataString(remoteSourceUri.AbsolutePath),
            "copy.txt");
        var localRoot = Path.Combine(Path.GetTempPath(), "brows-fileprotocol-" + Guid.NewGuid().ToString("N"));
        var localFile = Path.Combine(localRoot, "destination.txt");
        Directory.CreateDirectory(localRoot);
        File.WriteAllText(localFile, "local replacement");
        try {
            var io = new ProvidedIO {
                StreamSets = [
                    new Brows.Tests.TestFileSourceSet(localFile),
                    new Brows.Tests.TestEntryStreamSet(remoteSource)
                ]
            };

            var worked = await targetProvider.CopyProvidedIO.Work(
                [io],
                targetProvider,
                progress: null,
                CancellationToken.None);

            Assert.That(worked, Is.False);
            Assert.That(targetProvider.CopyWrites, Is.Zero);
            Assert.That(targetProvider.CopyDirectories, Is.Empty);
            Assert.That(remoteSource.OpenCount, Is.Zero);
            Assert.That(fileSystem.FileContents(remoteSourceUri), Is.EqualTo("remote source bytes must survive"));
        }
        finally {
            var directoryPath = Path.GetFullPath(localRoot);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var directoryIsWithinTemporaryRoot = directoryPath.StartsWith(
                temporaryRoot,
                StringComparison.OrdinalIgnoreCase);
            var directoryExists = Directory.Exists(directoryPath);
            var shouldDeleteDirectory = directoryIsWithinTemporaryRoot && directoryExists;
            if (shouldDeleteDirectory) {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
    }

    [Test]
    public async Task Work_CopiesRemoteFileToSiblingDirectory() {
        var fileSystem = new Brows.Tests.TestRemoteFileSystem();
        using var sourceProvider = new Brows.Tests.TestProvider(new Uri("sftp://user@host/root/"), fileSystem);
        var sourceUri = FileProtocolUri.Extend(sourceProvider.Uri, ["source.txt"], includeTrailingSlash: false);
        var targetFactory = new Brows.Tests.TestProviderFactory("sftp", fileSystem);
        using var targetProvider = await targetFactory.CreateInitialized("sftp://user@host/root/sibling/");
        fileSystem.SetFile(sourceUri, "valid sibling copy");
        var source = new Brows.Tests.TestRemoteStreamSource(
            fileSystem,
            sourceUri.ToString(),
            Uri.UnescapeDataString(sourceUri.AbsolutePath),
            "source.txt");
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet(source)]
        };

        var worked = await targetProvider.CopyProvidedIO.Work(
            [io],
            targetProvider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.True);
        Assert.That(targetProvider.CopyWrites, Is.EqualTo(1));
        Assert.That(fileSystem.FileContents(sourceUri), Is.EqualTo("valid sibling copy"));
        Assert.That(
            fileSystem.Read("/root/sibling/source.txt"),
            Is.EqualTo(Encoding.UTF8.GetBytes("valid sibling copy")));
    }

    [Test]
    public async Task Work_CopiesBetweenDifferentRemoteProtocols() {
        var fileSystem = new Brows.Tests.TestRemoteFileSystem();
        using var sourceProvider = new Brows.Tests.TestProvider(new Uri("ftp://user@host/root/"), fileSystem);
        var sourceUri = FileProtocolUri.Extend(sourceProvider.Uri, ["source.txt"], includeTrailingSlash: false);
        var targetFactory = new Brows.Tests.TestProviderFactory("sftp", fileSystem);
        using var targetProvider = await targetFactory.CreateInitialized("sftp://user@host/root/sibling/");
        fileSystem.SetFile(sourceUri, "cross-protocol copy");
        var source = new Brows.Tests.TestRemoteStreamSource(
            fileSystem,
            sourceUri.ToString(),
            Uri.UnescapeDataString(sourceUri.AbsolutePath),
            "source.txt");
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet(source)]
        };

        var worked = await targetProvider.CopyProvidedIO.Work(
            [io],
            targetProvider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.True);
        Assert.That(targetProvider.CopyWrites, Is.EqualTo(1));
        Assert.That(fileSystem.FileContents(sourceUri), Is.EqualTo("cross-protocol copy"));
        Assert.That(
            fileSystem.Read("/root/sibling/source.txt"),
            Is.EqualTo(Encoding.UTF8.GetBytes("cross-protocol copy")));
    }

    [Test]
    public async Task Work_MaterializesOneShotStreamSetAndKeepsReadinessUntilConsumption() {
        var fileSystem = new Brows.Tests.TestRemoteFileSystem();
        using var sourceProvider = new Brows.Tests.TestProvider(new Uri("sftp://user@host/root/"), fileSystem);
        var targetFactory = new Brows.Tests.TestProviderFactory("sftp", fileSystem);
        using var targetProvider = await targetFactory.CreateInitialized("sftp://user@host/sibling/");
        var sourceUri = new Uri("sftp://user@host/root/file.txt");
        fileSystem.SetFile(sourceUri, "prepared source");
        Brows.Tests.TestOneShotEntryStreamSet streamSet = null;
        var source = new Brows.Tests.TestRemoteStreamSource(
            fileSystem,
            sourceUri.ToString(),
            Uri.UnescapeDataString(sourceUri.AbsolutePath),
            "file.txt",
            readinessIsDisposed: () => streamSet.ReadinessIsDisposed);
        streamSet = new Brows.Tests.TestOneShotEntryStreamSet(source);
        var io = new ProvidedIO {
            StreamSets = [streamSet]
        };

        var worked = await targetProvider.CopyProvidedIO.Work(
            [io],
            targetProvider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.True);
        Assert.That(streamSet.EnumerationCount, Is.EqualTo(1));
        Assert.That(streamSet.ReadinessIsDisposed, Is.True);
        Assert.That(source.OpenCount, Is.EqualTo(1));
        Assert.That(fileSystem.Read("/sibling/file.txt"), Is.EqualTo(Encoding.UTF8.GetBytes("prepared source")));
    }

    [Test]
    public async Task Work_CopiesWhenLinuxPathDiffersByCase() {
        var fileSystem = new Brows.Tests.TestRemoteFileSystem();
        using var sourceProvider = new Brows.Tests.TestProvider(new Uri("sftp://user@host/root/"), fileSystem);
        var targetFactory = new Brows.Tests.TestProviderFactory("sftp", fileSystem);
        using var targetProvider = await targetFactory.CreateInitialized("sftp://user@host/root/");
        var sourceUri = new Uri("sftp://user@host/root/file.txt");
        fileSystem.SetFile(sourceUri, "lowercase source");
        var source = new Brows.Tests.TestRemoteStreamSource(
            fileSystem,
            sourceUri.ToString(),
            Uri.UnescapeDataString(sourceUri.AbsolutePath),
            "File.txt");
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet(source)]
        };

        var worked = await targetProvider.CopyProvidedIO.Work(
            [io],
            targetProvider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.True);
        Assert.That(targetProvider.CopyWrites, Is.EqualTo(1));
        Assert.That(fileSystem.FileContents(sourceUri), Is.EqualTo("lowercase source"));
        Assert.That(fileSystem.Read("/root/File.txt"), Is.EqualTo(Encoding.UTF8.GetBytes("lowercase source")));
    }

    [Test]
    public async Task Work_RejectsSftpSelfCopyWithRepeatedPathSeparators() {
        var fileSystem = new Brows.Tests.TestRemoteFileSystem();
        var sourceUri = new Uri("sftp://user@host/root//file.txt");
        using var targetProvider = new Brows.Tests.TestProvider(new Uri("sftp://user@host/root/"), fileSystem);
        fileSystem.SetFile(sourceUri, "repeated-separator source must survive");
        var source = new Brows.Tests.TestRemoteStreamSource(
            fileSystem,
            sourceUri.ToString(),
            Uri.UnescapeDataString(sourceUri.AbsolutePath),
            "file.txt");
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet(source)]
        };

        var worked = await targetProvider.CopyProvidedIO.Work(
            [io],
            targetProvider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.False);
        Assert.That(targetProvider.CopyWrites, Is.Zero);
        Assert.That(targetProvider.CopyDirectories, Is.Empty);
        Assert.That(source.OpenCount, Is.Zero);
        Assert.That(fileSystem.FileContents(sourceUri), Is.EqualTo("repeated-separator source must survive"));
    }

    [Test]
    public async Task Work_CopiesWhenLinuxUsernamesDifferByCase() {
        var sourceFileSystem = new Brows.Tests.TestRemoteFileSystem();
        var targetFileSystem = new Brows.Tests.TestRemoteFileSystem();
        using var sourceProvider = new Brows.Tests.TestProvider(new Uri("sftp://alice@host/root/"), sourceFileSystem);
        var targetFactory = new Brows.Tests.TestProviderFactory("sftp", targetFileSystem);
        using var targetProvider = await targetFactory.CreateInitialized("sftp://Alice@host/root/");
        var sourceUri = new Uri("sftp://alice@host/root/file.txt");
        sourceFileSystem.SetFile(sourceUri, "alice source");
        var source = new Brows.Tests.TestRemoteStreamSource(
            sourceFileSystem,
            sourceUri.ToString(),
            Uri.UnescapeDataString(sourceUri.AbsolutePath),
            "file.txt");
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet(source)]
        };

        var worked = await targetProvider.CopyProvidedIO.Work(
            [io],
            targetProvider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.True);
        Assert.That(targetProvider.CopyWrites, Is.EqualTo(1));
        Assert.That(sourceFileSystem.FileContents(sourceUri), Is.EqualTo("alice source"));
        Assert.That(targetFileSystem.Read("/root/file.txt"), Is.EqualTo(Encoding.UTF8.GetBytes("alice source")));
    }

    [TestCase("ftp")]
    [TestCase("ftps")]
    public async Task Work_RejectsAmbiguousFtpLoginRelativeAndServerAbsolutePaths(string scheme) {
        var fileSystem = new Brows.Tests.TestRemoteFileSystem();
        var sourceUri = new Uri($"{scheme}://user@host//root/file.txt");
        var targetUri = new Uri($"{scheme}://user@host/root/");
        var targetFactory = new Brows.Tests.TestProviderFactory(scheme, fileSystem);
        using var targetProvider = await targetFactory.CreateInitialized(targetUri.ToString());
        fileSystem.SetFile(sourceUri, "server absolute source");
        var source = new Brows.Tests.TestRemoteStreamSource(
            fileSystem,
            sourceUri.ToString(),
            Uri.UnescapeDataString(sourceUri.AbsolutePath),
            "file.txt");
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet(source)]
        };

        var worked = await targetProvider.CopyProvidedIO.Work(
            [io],
            targetProvider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.False);
        Assert.That(targetProvider.CopyWrites, Is.Zero);
        Assert.That(targetProvider.CopyDirectories, Is.Empty);
        Assert.That(source.OpenCount, Is.Zero);
        Assert.That(fileSystem.FileContents(sourceUri), Is.EqualTo("server absolute source"));
    }

    [TestCase("ftp")]
    [TestCase("ftps")]
    public async Task Work_RejectsDifferentFtpRootModesEvenWhenPathTextDiffers(string scheme) {
        var fileSystem = new Brows.Tests.TestRemoteFileSystem();
        var sourceUri = new Uri($"{scheme}://user@host/a.txt");
        using var targetProvider = new Brows.Tests.TestProvider(
            new Uri($"{scheme}://user@host//home/"),
            fileSystem);
        fileSystem.SetFile(sourceUri, "relative source bytes must survive");
        var source = new Brows.Tests.TestRemoteStreamSource(
            fileSystem,
            sourceUri.ToString(),
            Uri.UnescapeDataString(sourceUri.AbsolutePath),
            "copy.txt");
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet(source)]
        };

        var worked = await targetProvider.CopyProvidedIO.Work(
            [io],
            targetProvider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.False);
        Assert.That(targetProvider.CopyWrites, Is.Zero);
        Assert.That(targetProvider.CopyDirectories, Is.Empty);
        Assert.That(source.OpenCount, Is.Zero);
        Assert.That(fileSystem.FileContents(sourceUri), Is.EqualTo("relative source bytes must survive"));
    }

    [Test]
    public async Task Work_RejectsEquivalentExplicitFtpsUrisBeforeWriting() {
        var fileSystem = new Brows.Tests.TestRemoteFileSystem();
        var sourceUri = new UriBuilder("ftp://user@host:21/root/file.txt") {
            Scheme = "ftpes",
            Query = "brows-tls=explicit"
        }.Uri;
        using var targetProvider = new Brows.Tests.TestProvider(new Uri("ftp://user@host:21/root/"), fileSystem);
        fileSystem.SetFile(sourceUri, "explicit FTPS bytes must survive");
        var source = new Brows.Tests.TestRemoteStreamSource(
            fileSystem,
            sourceUri.ToString(),
            Uri.UnescapeDataString(sourceUri.AbsolutePath),
            "file.txt");
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet(source)]
        };

        var worked = await targetProvider.CopyProvidedIO.Work(
            [io],
            targetProvider,
            progress: null,
            CancellationToken.None);

        Assert.That(worked, Is.False);
        Assert.That(targetProvider.CopyWrites, Is.Zero);
        Assert.That(targetProvider.CopyDirectories, Is.Empty);
        Assert.That(source.OpenCount, Is.Zero);
        Assert.That(fileSystem.FileContents(sourceUri), Is.EqualTo("explicit FTPS bytes must survive"));
    }

    [Test]
    public async Task Work_CreatesParentDirectoriesForFlattenedLocalFiles() {
        var factory = new Brows.Tests.TestProviderFactory("ftp");
        using var provider = await factory.CreateInitialized("ftp://host/target/");
        var sourceRoot = Path.Combine(Path.GetTempPath(), "brows-fileprotocol-" + Guid.NewGuid().ToString("N"));
        try {
            var firstPath = Path.Combine(sourceRoot, "one", "first.txt");
            var secondPath = Path.Combine(sourceRoot, "two", "deeper", "second.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(firstPath));
            Directory.CreateDirectory(Path.GetDirectoryName(secondPath));
            File.WriteAllText(firstPath, "first");
            File.WriteAllText(secondPath, "second");
            var io = new ProvidedIO {
                StreamSets = [new Brows.Tests.TestFileSourceSet(firstPath, secondPath)]
            };

            var worked = await provider.CopyProvidedIO.Work(
                [io],
                provider,
                new Brows.Tests.TestOperationProgress(),
                CancellationToken.None);

            Assert.That(worked, Is.True);
            Assert.That(provider.CopyDirectories, Is.EqualTo(new[] {
                "/target/one",
                "/target/two",
                "/target/two/deeper"
            }));
            Assert.That(provider.CopyOutputs.Keys, Is.EquivalentTo(new[] {
                "/target/one/first.txt",
                "/target/two/deeper/second.txt"
            }));
            Assert.That(Encoding.UTF8.GetString(provider.CopyOutputs["/target/one/first.txt"].ToArray()),
                Is.EqualTo("first"));
            Assert.That(Encoding.UTF8.GetString(provider.CopyOutputs["/target/two/deeper/second.txt"].ToArray()),
                Is.EqualTo("second"));
            Assert.That(provider.Listed, Is.GreaterThan(0));
        }
        finally {
            var directoryPath = Path.GetFullPath(sourceRoot);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (directoryPath.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)) {
                if (Directory.Exists(directoryPath)) {
                    Directory.Delete(directoryPath, recursive: true);
                }
            }
        }
    }

    [Test]
    public async Task Work_ContinuesCopyingSiblingFilesWhenAWriteFails() {
        var factory = new Brows.Tests.TestProviderFactory("ftp");
        using var provider = await factory.CreateInitialized("ftp://host/target/");
        provider.FailCopyWriteAt = 2;
        var sourceRoot = Path.Combine(Path.GetTempPath(), "brows-fileprotocol-" + Guid.NewGuid().ToString("N"));
        try {
            var directory = Path.Combine(sourceRoot, "one");
            Directory.CreateDirectory(directory);
            var firstPath = Path.Combine(directory, "first.txt");
            var failedPath = Path.Combine(directory, "failed.txt");
            var lastPath = Path.Combine(directory, "last.txt");
            File.WriteAllText(firstPath, "first");
            File.WriteAllText(failedPath, "failed");
            File.WriteAllText(lastPath, "last");
            var io = new ProvidedIO {
                StreamSets = [new Brows.Tests.TestFileSourceSet(firstPath, failedPath, lastPath)]
            };

            var worked = await provider.CopyProvidedIO.Work(
                [io],
                provider,
                new Brows.Tests.TestOperationProgress(),
                CancellationToken.None);

            using (Assert.EnterMultipleScope()) {
                Assert.That(worked, Is.False);
                Assert.That(provider.CopyWrites, Is.EqualTo(3));
                Assert.That(provider.CopyOutputs.Keys, Is.EquivalentTo(new[] {
                    "/target/first.txt",
                    "/target/last.txt"
                }));
                Assert.That(Encoding.UTF8.GetString(provider.CopyOutputs["/target/first.txt"].ToArray()),
                    Is.EqualTo("first"));
                Assert.That(Encoding.UTF8.GetString(provider.CopyOutputs["/target/last.txt"].ToArray()),
                    Is.EqualTo("last"));
            }
        }
        finally {
            var directoryPath = Path.GetFullPath(sourceRoot);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (directoryPath.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)) {
                if (Directory.Exists(directoryPath)) {
                    Directory.Delete(directoryPath, recursive: true);
                }
            }
        }
    }

    [Test]
    public async Task Work_CopiesFlattenedLocalFilesAndRemoteStreamSetsTogether() {
        var factory = new Brows.Tests.TestProviderFactory("ftp");
        using var provider = await factory.CreateInitialized("ftp://host/target/");
        var sourceRoot = Path.Combine(Path.GetTempPath(), "brows-fileprotocol-" + Guid.NewGuid().ToString("N"));
        try {
            var firstPath = Path.Combine(sourceRoot, "one", "first.txt");
            var secondPath = Path.Combine(sourceRoot, "two", "second.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(firstPath));
            Directory.CreateDirectory(Path.GetDirectoryName(secondPath));
            File.WriteAllText(firstPath, "first local file");
            File.WriteAllText(secondPath, "second local file");
            var remoteEntry = new Brows.Tests.TestEntry(provider, "ftp://host/source/remote.txt") {
                NameValue = "remote.txt"
            };
            var remoteSource = new Brows.Tests.TestPayloadStreamSource(
                remoteEntry,
                "remote/remote.txt",
                "remote stream");
            var io = new ProvidedIO {
                StreamSets = [
                    new Brows.Tests.TestFileSourceSet(firstPath, secondPath),
                    new Brows.Tests.TestEntryStreamSet(remoteSource)
                ]
            };
            var progress = new Brows.Tests.TestOperationProgress();

            var worked = await provider.CopyProvidedIO.Work(
                [io],
                provider,
                progress,
                CancellationToken.None);

            Assert.That(worked, Is.True);
            Assert.That(provider.CopyOutputs.Keys, Is.EquivalentTo(new[] {
                "/target/one/first.txt",
                "/target/two/second.txt",
                "/target/remote/remote.txt"
            }));
            Assert.That(Encoding.UTF8.GetString(provider.CopyOutputs["/target/one/first.txt"].ToArray()),
                Is.EqualTo("first local file"));
            Assert.That(Encoding.UTF8.GetString(provider.CopyOutputs["/target/two/second.txt"].ToArray()),
                Is.EqualTo("second local file"));
            Assert.That(Encoding.UTF8.GetString(provider.CopyOutputs["/target/remote/remote.txt"].ToArray()),
                Is.EqualTo("remote stream"));
            var copiedBytes = Encoding.UTF8.GetByteCount("first local file") +
                              Encoding.UTF8.GetByteCount("second local file") +
                              Encoding.UTF8.GetByteCount("remote stream");
            using (Assert.EnterMultipleScope()) {
                Assert.That(progress.Kind, Is.EqualTo(OperationProgressKind.FileSize));
                Assert.That(progress.Progress, Is.EqualTo(copiedBytes));
                Assert.That(progress.Target, Is.EqualTo(copiedBytes));
            }
        }
        finally {
            var directoryPath = Path.GetFullPath(sourceRoot);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (directoryPath.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)) {
                if (Directory.Exists(directoryPath)) {
                    Directory.Delete(directoryPath, recursive: true);
                }
            }
        }
    }

    [Test]
    public async Task Work_CreatesEmptyLocalDirectoriesAndTheirAncestors() {
        var factory = new Brows.Tests.TestProviderFactory("ftp");
        using var provider = await factory.CreateInitialized("ftp://host/target/");
        var sourceRoot = Path.Combine(Path.GetTempPath(), "brows-fileprotocol-" + Guid.NewGuid().ToString("N"));
        var emptyDirectory = Path.Combine(sourceRoot, "one", "two", "empty");
        try {
            Directory.CreateDirectory(Path.Combine(emptyDirectory, "nested", "deep"));
            var io = new ProvidedIO {
                StreamSets = [new Brows.Tests.TestFileSourceSet(emptyDirectory)]
            };

            var worked = await provider.CopyProvidedIO.Work(
                [io],
                provider,
                progress: null,
                CancellationToken.None);

            Assert.That(worked, Is.True);
            Assert.That(provider.CopyDirectories, Is.EqualTo(new[] {
                "/target/empty",
                "/target/empty/nested",
                "/target/empty/nested/deep"
            }));
            Assert.That(provider.CopyWrites, Is.Zero);
            Assert.That(provider.Listed, Is.GreaterThan(0));
        }
        finally {
            var directoryPath = Path.GetFullPath(sourceRoot);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (directoryPath.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)) {
                if (Directory.Exists(directoryPath)) {
                    Directory.Delete(directoryPath, recursive: true);
                }
            }
        }
    }

    [Test]
    public async Task Work_PreservesEmptyBranchesBesideFlattenedLocalFiles() {
        var factory = new Brows.Tests.TestProviderFactory("ftp");
        using var provider = await factory.CreateInitialized("ftp://host/target/");
        var sourceRoot = Path.Combine(Path.GetTempPath(), "brows-fileprotocol-" + Guid.NewGuid().ToString("N"));
        var selectedDirectory = Path.Combine(sourceRoot, "selected");
        try {
            Directory.CreateDirectory(Path.Combine(selectedDirectory, "empty", "nested"));
            File.WriteAllText(Path.Combine(selectedDirectory, "payload.txt"), "payload");
            var io = new ProvidedIO {
                StreamSets = [new Brows.Tests.TestFileSourceSet(selectedDirectory)]
            };

            var worked = await provider.CopyProvidedIO.Work(
                [io],
                provider,
                progress: null,
                CancellationToken.None);

            Assert.That(worked, Is.True);
            Assert.That(provider.CopyDirectories, Is.EqualTo(new[] {
                "/target/selected",
                "/target/selected/empty",
                "/target/selected/empty/nested"
            }));
            Assert.That(provider.CopyOutputs.Keys, Is.EqualTo(new[] {
                "/target/selected/payload.txt"
            }));
            Assert.That(Encoding.UTF8.GetString(provider.CopyOutputs["/target/selected/payload.txt"].ToArray()),
                Is.EqualTo("payload"));
        }
        finally {
            var directoryPath = Path.GetFullPath(sourceRoot);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (directoryPath.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)) {
                if (Directory.Exists(directoryPath)) {
                    Directory.Delete(directoryPath, recursive: true);
                }
            }
        }
    }

    [Test]
    public async Task Work_PropagatesCancellationBeforeWritingStreamSources() {
        var factory = new Brows.Tests.TestProviderFactory("ftp");
        using var provider = await factory.CreateInitialized("ftp://host/target/");
        var copy = provider.CopyProvidedIO;
        var entry = new Brows.Tests.TestEntry(provider, "ftp://host/source/file.txt") {
            NameValue = "file.txt"
        };
        var source = new Brows.Tests.TestPayloadStreamSource(entry, "file.txt", "protocol data");
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet(source)]
        };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            copy.Work([io], provider, progress: null, cancellation.Token));
        Assert.That(provider.CopyWrites, Is.Zero);
    }

    [Test]
    public async Task Work_PropagatesCancellationDuringAStreamWrite() {
        var factory = new Brows.Tests.TestProviderFactory("ftp");
        using var provider = await factory.CreateInitialized("ftp://host/target/");
        var copy = provider.CopyProvidedIO;
        using var cancellation = new CancellationTokenSource();
        var progress = new Brows.Tests.TestOperationProgress(cancellation);
        var contents = new string('x', 200000);
        var entry = new Brows.Tests.TestEntry(provider, "ftp://host/source/file.txt") {
            NameValue = "file.txt"
        };
        var source = new Brows.Tests.TestPayloadStreamSource(entry, "file.txt", contents);
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet(source)]
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            copy.Work([io], provider, progress, cancellation.Token));

        var output = provider.CopyOutputs.Values.Single().ToArray();
        Assert.That(output.Length, Is.LessThan(Encoding.UTF8.GetByteCount(contents)));
        Assert.That(progress.Progress, Is.GreaterThan(0));
        Assert.That(progress.Progress, Is.EqualTo(output.Length));
    }

    [Test]
    public async Task Work_ContinuesCopyingSiblingStreamsWhenAWriteFails() {
        var factory = new Brows.Tests.TestProviderFactory("ftp");
        using var provider = await factory.CreateInitialized("ftp://host/target/");
        provider.FailCopyWriteAt = 2;
        var progress = new Brows.Tests.TestOperationProgress();
        var firstEntry = new Brows.Tests.TestEntry(provider, "ftp://host/source/first.txt") {
            NameValue = "first.txt"
        };
        var failedEntry = new Brows.Tests.TestEntry(provider, "ftp://host/source/failed.txt") {
            NameValue = "failed.txt"
        };
        var lastEntry = new Brows.Tests.TestEntry(provider, "ftp://host/source/last.txt") {
            NameValue = "last.txt"
        };
        var first = new Brows.Tests.TestPayloadStreamSource(firstEntry, "one/first.txt", "first");
        var failed = new Brows.Tests.TestPayloadStreamSource(failedEntry, "one/failed.txt", "failed");
        var last = new Brows.Tests.TestPayloadStreamSource(lastEntry, "one/last.txt", "last");
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet(first, failed, last)]
        };

        var worked = await provider.CopyProvidedIO.Work([io], provider, progress, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(worked, Is.False);
            Assert.That(provider.CopyWrites, Is.EqualTo(3));
            Assert.That(provider.CopyOutputs.Keys, Is.EquivalentTo(new[] {
                "/target/one/first.txt",
                "/target/one/last.txt"
            }));
            Assert.That(Encoding.UTF8.GetString(provider.CopyOutputs["/target/one/first.txt"].ToArray()),
                Is.EqualTo("first"));
            Assert.That(Encoding.UTF8.GetString(provider.CopyOutputs["/target/one/last.txt"].ToArray()),
                Is.EqualTo("last"));
        }
    }

    [Test]
    public async Task Work_ContinuesCopyingSiblingStreamsWhenAStreamCannotOpen() {
        var factory = new Brows.Tests.TestProviderFactory("ftp");
        using var provider = await factory.CreateInitialized("ftp://host/target/");
        var progress = new Brows.Tests.TestOperationProgress();
        var firstEntry = new Brows.Tests.TestEntry(provider, "ftp://host/source/first.txt") {
            NameValue = "first.txt"
        };
        var failedEntry = new Brows.Tests.TestEntry(provider, "ftp://host/source/failed.txt") {
            NameValue = "failed.txt"
        };
        var lastEntry = new Brows.Tests.TestEntry(provider, "ftp://host/source/last.txt") {
            NameValue = "last.txt"
        };
        var first = new Brows.Tests.TestPayloadStreamSource(firstEntry, "one/first.txt", "first");
        var failed = new Brows.Tests.TestPayloadStreamSource(
            failedEntry,
            "one/failed.txt",
            "failed",
            streamReadyFailure: new IOException("The source stream failed to open."));
        var last = new Brows.Tests.TestPayloadStreamSource(lastEntry, "one/last.txt", "last");
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet(first, failed, last)]
        };

        var worked = await provider.CopyProvidedIO.Work([io], provider, progress, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(worked, Is.False);
            Assert.That(provider.CopyWrites, Is.EqualTo(2));
            Assert.That(provider.CopyOutputs.Keys, Is.EquivalentTo(new[] {
                "/target/one/first.txt",
                "/target/one/last.txt"
            }));
            Assert.That(Encoding.UTF8.GetString(provider.CopyOutputs["/target/one/first.txt"].ToArray()),
                Is.EqualTo("first"));
            Assert.That(Encoding.UTF8.GetString(provider.CopyOutputs["/target/one/last.txt"].ToArray()),
                Is.EqualTo("last"));
        }
    }

    [Test]
    public async Task Work_PropagatesWriteFailuresAfterEarlierFilesWereCopied() {
        var factory = new Brows.Tests.TestProviderFactory("ftp");
        using var provider = await factory.CreateInitialized("ftp://host/target/");
        provider.FailCopyWriteAt = 2;
        var copy = provider.CopyProvidedIO;
        var firstEntry = new Brows.Tests.TestEntry(provider, "ftp://host/source/first.txt") {
            NameValue = "first.txt"
        };
        var secondEntry = new Brows.Tests.TestEntry(provider, "ftp://host/source/second.txt") {
            NameValue = "second.txt"
        };
        var first = new Brows.Tests.TestPayloadStreamSource(firstEntry, "one/first.txt", "first");
        var second = new Brows.Tests.TestPayloadStreamSource(secondEntry, "one/second.txt", "second");
        var io = new ProvidedIO {
            StreamSets = [new Brows.Tests.TestEntryStreamSet(first, second)]
        };

        await Assert.ThrowsAsync<IOException>(() =>
            copy.Work([io], provider, progress: null, CancellationToken.None));
        Assert.That(provider.CopyWrites, Is.EqualTo(2));
        Assert.That(provider.CopyOutputs.Keys, Is.EquivalentTo(new[] {
            "/target/one/first.txt"
        }));
        Assert.That(Encoding.UTF8.GetString(provider.CopyOutputs.Values.Single().ToArray()), Is.EqualTo("first"));
    }
}
