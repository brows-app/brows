using Brows.Composition;
using Brows.Entries;
using Brows.Providers;
using Brows.SevenZip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class SevenZipProviderTest {
    private SevenZipTestSandbox Sandbox;

    [SetUp]
    public void SetUp() {
        Sandbox = new();
    }

    [TearDown]
    public void TearDown() {
        Sandbox.Dispose();
    }

    [Test]
    public async Task CopyProvidedIO_CopiesFilesAndStreamsIntoTheTargetArchiveDirectory() {
        var archivePath = Sandbox.PathFor("target.7z");
        var root = Sandbox.WriteFile("root-source.txt", "root contents");
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "folder/existing.txt", Sandbox.WriteFile("existing.txt", "existing") },
            { "source.txt", root }
        }, null, null, CancellationToken.None);
        var factory = new SevenZipProviderFactory();
        var archive = await SevenZipID.Parse($"7z:1|f={archivePath}|d=folder", factory, CancellationToken.None);
        try {
            var provider = new SevenZipProvider(archive, factory);
            var filesystemIO = new ProvidedIO {
                StreamSets = new[] { EntryStreamSet.FromFiles(new[] {
                    Sandbox.WriteFile("filesystem-source.txt", "filesystem contents")
                }) }
            };
            var streamIO = new ProvidedIO {
                StreamSets = new[] { new TestEntryStreamSet("source.txt", "source contents") }
            };
            var service = CopyService();
            var copiedFiles = await service.Work(new[] { filesystemIO }, provider, null, CancellationToken.None);
            var copiedStreams = await service.Work(new[] { streamIO }, provider, null, CancellationToken.None);

            using var result = SevenZipArchive.Open(archivePath);
            using (Assert.EnterMultipleScope()) {
                Assert.That(copiedFiles, Is.True);
                Assert.That(copiedStreams, Is.True);
            }
            Assert.That(result.Entries.Select(entry => entry.Path), Is.EquivalentTo(new[] {
                "folder/existing.txt",
                "folder/filesystem-source.txt",
                "folder/source.txt",
                "source.txt"
            }));
            var rootIndex = result.IndexOf("source.txt");
            var rootCopy = Sandbox.PathFor("root-copy.txt");
            result.Extract(rootIndex, rootCopy, overwrite: false);
            Assert.That(File.ReadAllText(rootCopy), Is.EqualTo("root contents"));
        }
        finally {
            archive.Release();
        }
    }

    [Test]
    public async Task CopyProvidedIO_KeepsTheSelectedDirectoryInAFileOnlyCopy() {
        var archivePath = Sandbox.PathFor("target.7z");
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "existing.txt", Sandbox.WriteFile("existing.txt", "existing") }
        }, null, null, CancellationToken.None);
        var sourceDirectory = Sandbox.CreateDirectory("source");
        Sandbox.WriteFile("source/file.txt", "source contents");
        var factory = new SevenZipProviderFactory();
        var archive = await SevenZipID.Parse(archivePath, factory, CancellationToken.None);
        try {
            var provider = new SevenZipProvider(archive, factory);
            var io = new ProvidedIO {
                StreamSets = new[] { EntryStreamSet.FromFiles(new[] { sourceDirectory }) }
            };
            var copied = await CopyService().Work(new[] { io }, provider, null, CancellationToken.None);

            using var result = SevenZipArchive.Open(archivePath);
            Assert.That(copied, Is.True);
            Assert.That(result.Entries.Select(entry => entry.Path), Is.EquivalentTo(new[] {
                "existing.txt",
                "source/file.txt"
            }));
        }
        finally {
            archive.Release();
        }
    }

    [Test]
    public async Task CopyProvidedIO_CopiesAnEmptyFilesystemDirectoryIntoTheArchive() {
        var archivePath = Sandbox.PathFor("target.7z");
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "existing.txt", Sandbox.WriteFile("existing.txt", "existing") }
        }, null, null, CancellationToken.None);
        var sourceDirectory = Sandbox.CreateDirectory("empty");
        var factory = new SevenZipProviderFactory();
        var archive = await SevenZipID.Parse(archivePath, factory, CancellationToken.None);
        try {
            var provider = new SevenZipProvider(archive, factory);
            var io = new ProvidedIO {
                StreamSets = new[] { EntryStreamSet.FromFiles(new[] { sourceDirectory }) }
            };
            var copied = await CopyService().Work(new[] { io }, provider, null, CancellationToken.None);

            using var result = SevenZipArchive.Open(archivePath);
            using (Assert.EnterMultipleScope()) {
                Assert.That(copied, Is.True);
                Assert.That(result.Entries.Select(entry => entry.Path), Is.EquivalentTo(new[] {
                    "empty",
                    "existing.txt"
                }));
                Assert.That(result.Entries.Single(entry => entry.Path == "empty").IsDirectory, Is.True);
            }
        }
        finally {
            archive.Release();
        }
    }

    [Test]
    public async Task CopyProvidedIO_PreservesEmptyDescendantsAlongsideFilesIntoTheArchive() {
        var archivePath = Sandbox.PathFor("target.7z");
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "existing.txt", Sandbox.WriteFile("existing.txt", "existing") }
        }, null, null, CancellationToken.None);
        var sourceDirectory = Sandbox.CreateDirectory("source");
        Sandbox.CreateDirectory("source/empty-child");
        Sandbox.WriteFile("source/file.txt", "content");
        var factory = new SevenZipProviderFactory();
        var archive = await SevenZipID.Parse(archivePath, factory, CancellationToken.None);
        try {
            var provider = new SevenZipProvider(archive, factory);
            var io = new ProvidedIO {
                StreamSets = new[] { EntryStreamSet.FromFiles(new[] { sourceDirectory }) }
            };
            var copied = await CopyService().Work(new[] { io }, provider, null, CancellationToken.None);

            using var result = SevenZipArchive.Open(archivePath);
            Assert.That(copied, Is.True);
            Assert.That(result.Entries.Select(entry => entry.Path), Is.EquivalentTo(new[] {
                    "source/empty-child",
                "existing.txt",
                    "source/file.txt"
            }));
                Assert.That(result.Entries.Single(entry => entry.Path == "source/empty-child").IsDirectory, Is.True);
        }
        finally {
            archive.Release();
        }
    }

    [Test]
    public async Task CopyProvidedIO_CopiesAnEmptyArchiveDirectoryToTheFileSystem() {
        var archivePath = Sandbox.PathFor("directory-source.7z");
        SevenZipArchive.Create(archivePath, new[] {
            new SevenZipUpdateItem("empty", null, true, 0, null, 0)
        }, null);
        var factory = new SevenZipProviderFactory();
        var archive = await SevenZipID.Parse(archivePath, factory, CancellationToken.None);
        var destination = Sandbox.CreateDirectory("destination");
        var fileSystemProvider = CreateFileSystemProvider(destination);
        try {
            var provider = new SevenZipProvider(archive, factory);
            var info = (await archive.EntryInfo(CancellationToken.None))
                .Single(item => item.Kind == SevenZipEntryKind.Path);
            var source = new SevenZipStreamSource(new SevenZipEntry(provider, info));
            var io = new ProvidedIO {
                StreamSets = new[] { new SevenZipStreamSet(new[] { source }) }
            };
            var service = FileSystemCopyService(fileSystemProvider.GetType());
            var copied = await service.Work(new[] { io }, (IProvider)fileSystemProvider, null, CancellationToken.None);

            Assert.That(copied, Is.True);
            Assert.That(Directory.Exists(Path.Combine(destination, "empty")), Is.True);
        }
        finally {
            archive.Release();
            ((IDisposable)fileSystemProvider).Dispose();
        }
    }

    private static ICopyProvidedIO CopyService() {
        var type = typeof(SevenZipProvider).GetNestedType("CopyProvidedIO", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The SevenZip copy service was not found.");
        return (ICopyProvidedIO)Activator.CreateInstance(type, nonPublic: true);
    }

    private static object CreateFileSystemProvider(string directory) {
        var assembly = Assembly.Load("Brows.FileSystem");
        var factoryType = assembly.GetType("Brows.FileSystemProviderFactory", throwOnError: true);
        var providerType = assembly.GetType("Brows.FileSystemProvider", throwOnError: true);
        var factory = Activator.CreateInstance(factoryType, nonPublic: true);
        return Activator.CreateInstance(
            providerType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { factory, new DirectoryInfo(directory), false, 0 },
            culture: null);
    }

    private static ICopyProvidedIO FileSystemCopyService(Type providerType) {
        var type = providerType.GetNestedType("CopyProvidedIO", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The file-system copy service was not found.");
        return (ICopyProvidedIO)Activator.CreateInstance(type, nonPublic: true);
    }

    private sealed class TestEntryStreamSet : IEntryStreamSet {
        private readonly TestEntryStreamSource Source;

        public TestEntryStreamSet(string relativePath, string contents) {
            Source = new TestEntryStreamSource(relativePath, contents);
        }

        public IEntryStreamReady StreamSourceReady() {
            return new EntryStreamReady();
        }

        public IEnumerable<string> FileSource() {
            return Array.Empty<string>();
        }

        public async IAsyncEnumerable<IEntryStreamSource> StreamSource(
            [EnumeratorCancellation] CancellationToken token) {
            token.ThrowIfCancellationRequested();
            yield return Source;
            await Task.CompletedTask;
        }
    }

    private sealed class TestEntryStreamSource : IEntryStreamSource {
        private readonly byte[] Contents;

        public string EntryID => "provider-test";
        public string EntryName => RelativePath;
        public string RelativePath { get; }
        public string SourceFile => null;
        public string SourceDirectory => null;
        public long StreamLength => Contents.Length;
        public Stream Stream { get; private set; }

        public TestEntryStreamSource(string relativePath, string contents) {
            RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
            Contents = Encoding.UTF8.GetBytes(contents ?? throw new ArgumentNullException(nameof(contents)));
        }

        public Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            token.ThrowIfCancellationRequested();
            Stream = new MemoryStream(Contents, writable: false);
            return Task.FromResult<IEntryStreamReady>(new EntryStreamReady());
        }
    }
}
