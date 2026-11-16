using Brows.Composition;
using Brows.Entries;
using Brows.Panels;
using Brows.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed partial class ZipProviderTest {
    private sealed class TestPanelDrop : IPanelDrop {
        public object Target => null;
        public IReadOnlyList<string> CopyFiles { get; }
        public IReadOnlyList<string> MoveFiles => Array.Empty<string>();

        public TestPanelDrop(IEnumerable<string> copyFiles) {
            CopyFiles = copyFiles.ToArray();
        }
    }

    private sealed class TestStreamSource : IEntryStreamSource {
        private readonly byte[] Contents;

        public string EntryID => RelativePath;
        public string EntryName => RelativePath;
        public string RelativePath { get; }
        public string SourceFile => null;
        public string SourceDirectory => null;
        public long StreamLength => Contents.Length;
        public Stream Stream { get; private set; }

        public TestStreamSource(string relativePath, string contents) {
            RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
            Contents = Encoding.UTF8.GetBytes(contents ?? throw new ArgumentNullException(nameof(contents)));
        }

        public Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            token.ThrowIfCancellationRequested();
            Stream = new MemoryStream(Contents, writable: false);
            return Task.FromResult<IEntryStreamReady>(new EntryStreamReady());
        }
    }

    private sealed class TestStreamSet : IEntryStreamSet {
        private readonly IReadOnlyList<IEntryStreamSource> Sources;

        public TestStreamSet(IEnumerable<IEntryStreamSource> sources) {
            Sources = sources.ToArray();
        }

        public IEntryStreamReady StreamSourceReady() {
            return new EntryStreamReady();
        }

        public IEnumerable<string> FileSource() {
            return Array.Empty<string>();
        }

        public async IAsyncEnumerable<IEntryStreamSource> StreamSource(
            [EnumeratorCancellation] CancellationToken token) {
            foreach (var source in Sources) {
                token.ThrowIfCancellationRequested();
                yield return source;
            }
            await Task.CompletedTask;
        }
    }

    private ZipTestSandbox Sandbox;

    private static void AddFile(ZipArchive archive, string name, string contents) {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(contents);
        stream.Write(bytes);
    }

    private static ICopyProvidedIO CopyService() {
        var type = typeof(ZipProvider).GetNestedType("CopyProvidedIO", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The ZIP copy service was not found.");
        return (ICopyProvidedIO)Activator.CreateInstance(type, nonPublic: true);
    }

    private static IProvider CreateFileSystemProvider(string directory) {
        var assembly = Assembly.Load("Brows.FileSystem");
        var factoryType = assembly.GetType("Brows.FileSystemProviderFactory", throwOnError: true);
        var providerType = assembly.GetType("Brows.FileSystemProvider", throwOnError: true);
        var factory = Activator.CreateInstance(factoryType, nonPublic: true);
        return (IProvider)Activator.CreateInstance(
            providerType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] {
                factory,
                new DirectoryInfo(directory),
                true,
                0
            },
            culture: null);
    }

    private static ICopyProvidedIO FileSystemCopyService() {
        var assembly = Assembly.Load("Brows.FileSystem");
        var type = assembly.GetType("Brows.FileSystemProvider+CopyProvidedIO", throwOnError: true);
        return (ICopyProvidedIO)Activator.CreateInstance(type, nonPublic: true);
    }

    private static string ReadEntry(ZipArchive archive, string name) {
        using var stream = archive.GetEntry(name)?.Open()
            ?? throw new InvalidOperationException($"The ZIP entry '{name}' was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [SetUp]
    public void SetUp() {
        Sandbox = new();
    }

    [TearDown]
    public void TearDown() {
        Sandbox.Dispose();
    }

    [Test]
    public async Task CopyProvidedIO_CopiesStreamsAndDropsIntoTheTargetArchiveDirectory() {
        var archivePath = Sandbox.PathFor("target.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create)) {
            AddFile(archive, "same.txt", "root contents");
            AddFile(archive, "folder/same.txt", "old folder contents");
            AddFile(archive, "drop.txt", "root drop contents");
            AddFile(archive, "folder/drop.txt", "old folder drop contents");
        }
        var streamSource = new TestStreamSource("same.txt", "new folder contents");
        var factory = new ZipProviderFactory();
        var id = await ZipID.Parse(ZipTestSandbox.ProviderID(archivePath, "folder"), factory, CancellationToken.None);
        var provider = new ZipProvider(id, factory);
        try {
            var copied = await CopyService().Work(new[] {
                new ProvidedIO {
                    StreamSets = new[] { new TestStreamSet(new[] { streamSource }) }
                }
            }, provider, null, CancellationToken.None);
            var dropPath = Sandbox.PathFor("drop.txt");
            File.WriteAllText(dropPath, "new folder drop contents", new UTF8Encoding(false));
            var dropped = await ((IProvider)provider).Drop(
                new TestPanelDrop(new[] { dropPath }),
                null,
                CancellationToken.None);

            using var result = ZipFile.OpenRead(archivePath);
            using (Assert.EnterMultipleScope()) {
                Assert.That(copied, Is.True);
                Assert.That(dropped, Is.True);
                Assert.That(ReadEntry(result, "same.txt"), Is.EqualTo("root contents"));
                Assert.That(ReadEntry(result, "folder/same.txt"), Is.EqualTo("new folder contents"));
                Assert.That(ReadEntry(result, "drop.txt"), Is.EqualTo("root drop contents"));
                Assert.That(ReadEntry(result, "folder/drop.txt"), Is.EqualTo("new folder drop contents"));
            }
        }
        finally {
            provider.Dispose();
            id.Release();
        }
    }

    [Test]
    public async Task CopyProvidedIO_PreservesEmptyFilesystemDirectories() {
        var archivePath = Sandbox.PathFor("target.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create)) {
            AddFile(archive, "folder/existing.txt", "existing");
        }
        var emptyDirectory = Sandbox.PathFor("empty-source");
        Directory.CreateDirectory(emptyDirectory);
        var sourceDirectory = Sandbox.PathFor("source");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(Path.Combine(sourceDirectory, "empty-child"));
        Directory.CreateDirectory(Path.Combine(sourceDirectory, "child", "deeper", "empty"));
        Directory.CreateDirectory(Path.Combine(sourceDirectory, "branch", "empty"));
        File.WriteAllText(Path.Combine(sourceDirectory, "file.txt"), "contents", new UTF8Encoding(false));
        var directoryOnly = Sandbox.PathFor("directory-only");
        Directory.CreateDirectory(Path.Combine(directoryOnly, "child", "deeper", "empty"));
        var factory = new ZipProviderFactory();
        var id = await ZipID.Parse(ZipTestSandbox.ProviderID(archivePath, "folder"), factory, CancellationToken.None);
        var provider = new ZipProvider(id, factory);
        try {
            var copiedEmpty = await CopyService().Work(new[] {
                new ProvidedIO {
                    StreamSets = new[] { EntryStreamSet.FromFiles(new[] { emptyDirectory }) }
                }
            }, provider, null, CancellationToken.None);
            var copiedTree = await CopyService().Work(new[] {
                new ProvidedIO {
                    StreamSets = new[] { EntryStreamSet.FromFiles(new[] { sourceDirectory }) }
                }
            }, provider, null, CancellationToken.None);
            var copiedDirectoryOnlyTree = await CopyService().Work(new[] {
                new ProvidedIO {
                    StreamSets = new[] { EntryStreamSet.FromFiles(new[] { directoryOnly }) }
                }
            }, provider, null, CancellationToken.None);

            using var result = ZipFile.OpenRead(archivePath);
            var directoryEntries = result.Entries
                .Where(entry => entry.FullName.EndsWith('/'))
                .Select(entry => entry.FullName)
                .ToList();
            using (Assert.EnterMultipleScope()) {
                Assert.That(copiedEmpty, Is.True);
                Assert.That(copiedTree, Is.True);
                Assert.That(copiedDirectoryOnlyTree, Is.True);
                Assert.That(directoryEntries, Does.Contain("folder/empty-source/"));
                Assert.That(directoryEntries, Does.Contain("folder/source/empty-child/"));
                Assert.That(directoryEntries, Does.Contain("folder/source/child/deeper/empty/"));
                Assert.That(directoryEntries, Does.Contain("folder/source/branch/empty/"));
                Assert.That(directoryEntries, Does.Contain("folder/directory-only/child/deeper/empty/"));
                Assert.That(ReadEntry(result, "folder/source/file.txt"), Is.EqualTo("contents"));
            }
        }
        finally {
            provider.Dispose();
            id.Release();
        }
    }

    [Test]
    public async Task CopyProvidedIO_PreservesSubtreesForMultipleEmptyDirectorySelections() {
        var archivePath = Sandbox.PathFor("target.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create)) {
            AddFile(archive, "folder/existing.txt", "existing");
        }
        var selectionRoot = Sandbox.PathFor("selections");
        var firstDirectory = Path.Combine(selectionRoot, "first");
        var secondDirectory = Path.Combine(selectionRoot, "second");
        Directory.CreateDirectory(Path.Combine(firstDirectory, "child", "deeper", "empty"));
        Directory.CreateDirectory(Path.Combine(secondDirectory, "branch", "empty"));
        var factory = new ZipProviderFactory();
        var id = await ZipID.Parse(ZipTestSandbox.ProviderID(archivePath, "folder"), factory, CancellationToken.None);
        var provider = new ZipProvider(id, factory);
        try {
            var copied = await CopyService().Work(new[] {
                new ProvidedIO {
                    StreamSets = new[] {
                        EntryStreamSet.FromFiles(new[] { firstDirectory, secondDirectory })
                    }
                }
            }, provider, null, CancellationToken.None);

            using var result = ZipFile.OpenRead(archivePath);
            var directoryEntries = result.Entries
                .Where(entry => entry.FullName.EndsWith('/'))
                .Select(entry => entry.FullName)
                .ToList();
            using (Assert.EnterMultipleScope()) {
                Assert.That(copied, Is.True);
                Assert.That(directoryEntries, Does.Contain("folder/first/child/deeper/empty/"));
                Assert.That(directoryEntries, Does.Contain("folder/second/branch/empty/"));
            }
        }
        finally {
            provider.Dispose();
            id.Release();
        }
    }

    [Test]
    public async Task CopyProvidedIO_PreservesEmptyArchiveDirectoryMarkers() {
        var sourceArchivePath = Sandbox.PathFor("source.zip");
        using (var sourceArchive = ZipFile.Open(sourceArchivePath, ZipArchiveMode.Create)) {
            sourceArchive.CreateEntry("empty/");
        }
        var targetArchivePath = Sandbox.PathFor("target.zip");
        using (var targetArchive = ZipFile.Open(targetArchivePath, ZipArchiveMode.Create)) {
            AddFile(targetArchive, "folder/existing.txt", "existing");
        }
        var sourceFactory = new ZipProviderFactory();
        var sourceID = await ZipID.Parse(sourceArchivePath, sourceFactory, CancellationToken.None);
        var targetFactory = new ZipProviderFactory();
        var targetID = await ZipID.Parse(
            ZipTestSandbox.ProviderID(targetArchivePath, "folder"),
            targetFactory,
            CancellationToken.None);
        var sourceProvider = new ZipProvider(sourceID, sourceFactory);
        var targetProvider = new ZipProvider(targetID, targetFactory);
        try {
            var info = (await sourceID.ArchivePath.EntryInfo(CancellationToken.None)).Items
                .Single(item => item.Kind == ZipEntryKind.Path);
            var source = new ZipStreamSource(new ZipEntry(sourceProvider, info));
            var copied = await CopyService().Work(new[] {
                new ProvidedIO {
                    StreamSets = new[] { new ZipStreamSet(new[] { source }) }
                }
            }, targetProvider, null, CancellationToken.None);

            using var result = ZipFile.OpenRead(targetArchivePath);
            using (Assert.EnterMultipleScope()) {
                Assert.That(copied, Is.True);
                Assert.That(source.SourceDirectory, Is.EqualTo("empty"));
                Assert.That(result.Entries.Select(entry => entry.FullName), Does.Contain("folder/empty/"));
            }
        }
        finally {
            sourceProvider.Dispose();
            sourceID.Release();
            targetProvider.Dispose();
            targetID.Release();
        }
    }

    [Test]
    public async Task CopyProvidedIO_ExportsArchiveDirectoryMarkersToTheFilesystem() {
        var sourceArchivePath = Sandbox.PathFor("source.zip");
        using (var sourceArchive = ZipFile.Open(sourceArchivePath, ZipArchiveMode.Create)) {
            sourceArchive.CreateEntry("folder/");
            sourceArchive.CreateEntry("folder/empty/");
            AddFile(sourceArchive, "folder/nested/file.txt", "exported archive contents");
        }
        var targetDirectory = Sandbox.PathFor("filesystem-target");
        Directory.CreateDirectory(targetDirectory);
        var sourceFactory = new ZipProviderFactory();
        var sourceID = await ZipID.Parse(sourceArchivePath, sourceFactory, CancellationToken.None);
        var sourceProvider = new ZipProvider(sourceID, sourceFactory);
        var targetProvider = CreateFileSystemProvider(targetDirectory);
        try {
            var directoryPrefix = "folder" + Path.DirectorySeparatorChar;
            var infos = (await sourceID.ArchivePath.EntryInfo(CancellationToken.None)).Items
                .Where(info =>
                    info.Name.Normalized == "folder" ||
                    info.Name.Normalized.StartsWith(directoryPrefix, StringComparison.Ordinal))
                .ToList();
            var sources = infos
                .Select(info => new ZipStreamSource(new ZipEntry(sourceProvider, info)))
                .ToList();
            var copied = await FileSystemCopyService().Work(new[] {
                new ProvidedIO {
                    StreamSets = new[] { new ZipStreamSet(sources) }
                }
            }, targetProvider, null, CancellationToken.None);

            var exportedDirectory = Path.Combine(targetDirectory, "folder");
            var exportedEmptyDirectory = Path.Combine(exportedDirectory, "empty");
            var exportedFile = Path.Combine(exportedDirectory, "nested", "file.txt");
            using (Assert.EnterMultipleScope()) {
                Assert.That(copied, Is.True);
                Assert.That(Directory.Exists(exportedDirectory), Is.True);
                Assert.That(Directory.Exists(exportedEmptyDirectory), Is.True);
                Assert.That(File.ReadAllText(exportedFile), Is.EqualTo("exported archive contents"));
            }
        }
        finally {
            targetProvider.Dispose();
            sourceProvider.Dispose();
            sourceID.Release();
        }
    }
}
