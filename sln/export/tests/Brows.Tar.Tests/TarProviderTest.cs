using Brows.Composition;
using Brows.Entries;
using Brows.Providers;
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
internal sealed partial class TarProviderTest {
    private TarTestSandbox Sandbox;

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
        var archivePath = Sandbox.PathFor("target.tar");
        var root = Sandbox.WriteFile("root-source.txt", "root contents");
        await TarArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "folder/existing.txt", Sandbox.WriteFile("existing.txt", "existing") },
            { "source.txt", root }
        }, null, CancellationToken.None);
        var factory = new TarProviderFactory();
        var archive = await TarID.Parse($"tar:1|f={archivePath}|d=folder", factory, CancellationToken.None);
        try {
            var provider = new TarProvider(archive, factory);
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

            var items = await archive.ArchivePath.EntryInfo(CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(copiedFiles, Is.True);
                Assert.That(copiedStreams, Is.True);
            }
            Assert.That(items.Items
                .Where(item => item.Kind == TarEntryKind.File)
                .Select(item => item.Name.Original), Is.EquivalentTo(new[] {
                "folder/existing.txt",
                "folder/filesystem-source.txt",
                "folder/source.txt",
                "source.txt"
            }));
            var rootCopy = Sandbox.PathFor("root-copy.txt");
            await items.Items.Single(item => item.Name.Original == "source.txt")
                .Extract(rootCopy, overwrite: false, CancellationToken.None);
            Assert.That(File.ReadAllText(rootCopy), Is.EqualTo("root contents"));
        }
        finally {
            archive.Release();
        }
    }

    [Test]
    public async Task CopyProvidedIO_KeepsTheSelectedDirectoryInAFileOnlyCopy() {
        var archivePath = Sandbox.PathFor("target.tar");
        await TarArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "existing.txt", Sandbox.WriteFile("existing.txt", "existing") }
        }, null, CancellationToken.None);
        var sourceDirectory = Sandbox.CreateDirectory("source");
        Sandbox.WriteFile("source/file.txt", "source contents");
        var factory = new TarProviderFactory();
        var archive = await TarID.Parse(archivePath, factory, CancellationToken.None);
        try {
            var provider = new TarProvider(archive, factory);
            var io = new ProvidedIO {
                StreamSets = new[] { EntryStreamSet.FromFiles(new[] { sourceDirectory }) }
            };
            var copied = await CopyService().Work(new[] { io }, provider, null, CancellationToken.None);

            var items = await archive.ArchivePath.EntryInfo(CancellationToken.None);
            Assert.That(copied, Is.True);
            Assert.That(items.Items
                .Where(item => item.Kind == TarEntryKind.File)
                .Select(item => item.Name.Original), Is.EquivalentTo(new[] {
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
        var archivePath = Sandbox.PathFor("target.tar");
        await TarArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "existing.txt", Sandbox.WriteFile("existing.txt", "existing") }
        }, null, CancellationToken.None);
        var sourceDirectory = Sandbox.CreateDirectory("empty");
        var factory = new TarProviderFactory();
        var archive = await TarID.Parse(archivePath, factory, CancellationToken.None);
        try {
            var provider = new TarProvider(archive, factory);
            var io = new ProvidedIO {
                StreamSets = new[] { EntryStreamSet.FromFiles(new[] { sourceDirectory }) }
            };
            var copied = await CopyService().Work(new[] { io }, provider, null, CancellationToken.None);

            var items = await archive.ArchivePath.EntryInfo(CancellationToken.None);
            Assert.That(copied, Is.True);
            Assert.That(items.Items.Select(item => item.Name.Original), Is.EquivalentTo(new[] {
                "empty",
                "existing.txt"
            }));
        }
        finally {
            archive.Release();
        }
    }

    [Test]
    public async Task CopyProvidedIO_PreservesEmptyDescendantsAlongsideFilesIntoTheArchive() {
        var archivePath = Sandbox.PathFor("target.tar");
        await TarArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "existing.txt", Sandbox.WriteFile("existing.txt", "existing") }
        }, null, CancellationToken.None);
        var sourceDirectory = Sandbox.CreateDirectory("source");
        Sandbox.CreateDirectory("source/empty-child");
        Sandbox.WriteFile("source/file.txt", "content");
        var factory = new TarProviderFactory();
        var archive = await TarID.Parse(archivePath, factory, CancellationToken.None);
        try {
            var provider = new TarProvider(archive, factory);
            var io = new ProvidedIO {
                StreamSets = new[] { EntryStreamSet.FromFiles(new[] { sourceDirectory }) }
            };
            var copied = await CopyService().Work(new[] { io }, provider, null, CancellationToken.None);

            var items = await archive.ArchivePath.EntryInfo(CancellationToken.None);
            var names = items.Items.Select(item => item.Name.Original);
            Assert.That(copied, Is.True);
            using (Assert.EnterMultipleScope()) {
                Assert.That(items.Items
                    .Where(item => item.Kind == TarEntryKind.File)
                    .Select(item => item.Name.Original), Is.EquivalentTo(new[] {
                "existing.txt",
                "source/file.txt"
            }));
                Assert.That(names, Does.Contain("source/empty-child"));
            }
        }
        finally {
            archive.Release();
        }
    }

    private static ICopyProvidedIO CopyService() {
        var type = typeof(TarProvider).GetNestedType("CopyProvidedIO", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The Tar copy service was not found.");
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
