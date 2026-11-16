using Brows.Entries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Brows.Composition;

[TestFixture]
internal sealed partial class ClipboardSetIOTest {
    private sealed class TestClipboardSetIOData : IClipboardSetIOData {
        public bool MoveOnPaste { get; init; }
    }

    private sealed class TestClipboardGetIOData : IClipboardGetIOData {
        public bool MoveOnPaste { get; set; }
    }

    private sealed class TestStreamReady : IEntryStreamReady {
        private readonly Stream Stream;
        private readonly TestStreamSource Source;

        public TestStreamReady(Stream stream, TestStreamSource source) {
            Stream = stream;
            Source = source;
        }

        public void Dispose() {
            Stream.Dispose();
            Source.ReadyDisposed = true;
        }
    }

    private sealed class TestStreamSource : IEntryStreamSource {
        private readonly byte[] Contents;
        private readonly Exception Failure;

        public string EntryID => "clipboard-test";
        public string EntryName => RelativePath;
        public string RelativePath { get; }
        public string SourceFile => null;
        public string SourceDirectory { get; }
        public long StreamLength => Contents.Length;
        public Stream Stream { get; private set; }
        public bool ReadyDisposed { get; set; }

        public TestStreamSource(
            string relativePath,
            string contents,
            Exception failure = null,
            string sourceDirectory = null) {
            RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
            Contents = Encoding.UTF8.GetBytes(contents ?? throw new ArgumentNullException(nameof(contents)));
            Failure = failure;
            SourceDirectory = sourceDirectory;
        }

        public Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            token.ThrowIfCancellationRequested();
            if (Failure is not null) {
                throw Failure;
            }
            Stream = new MemoryStream(Contents, writable: false);
            return Task.FromResult<IEntryStreamReady>(new TestStreamReady(Stream, this));
        }
    }

    private sealed class TestStreamSet : IEntryStreamSet {
        private readonly IReadOnlyList<TestStreamSource> Sources;
        private readonly Exception Failure;
        private readonly IReadOnlyList<string> DirectFiles;
        private readonly Action AfterSources;

        public TestStreamSet(
            IEnumerable<TestStreamSource> sources,
            Exception failure = null,
            IEnumerable<string> directFiles = null,
            Action afterSources = null) {
            Sources = sources.ToArray();
            Failure = failure;
            DirectFiles = directFiles?.ToArray() ?? Array.Empty<string>();
            AfterSources = afterSources;
        }

        public IEntryStreamReady StreamSourceReady() {
            return new EntryStreamReady();
        }

        public IEnumerable<string> FileSource() {
            return DirectFiles;
        }

        public async IAsyncEnumerable<IEntryStreamSource> StreamSource(
            [EnumeratorCancellation] CancellationToken token) {
            token.ThrowIfCancellationRequested();
            if (Failure is not null) {
                throw Failure;
            }
            foreach (var source in Sources) {
                yield return source;
            }
            AfterSources?.Invoke();
            await Task.CompletedTask;
        }
    }

    private ClipboardTestTransport Transport;
    private string PreparedPath;

    private static string GetStagingRoot(string path) {
        var fullRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "BrowsClipboard"));
        var fullPath = Path.GetFullPath(path);
        var relativePath = Path.GetRelativePath(fullRoot, fullPath);
        var escapedRoot = relativePath == ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relativePath);
        if (escapedRoot) {
            return null;
        }
        var components = relativePath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        if (components.Length < 3 || Guid.TryParseExact(components[0], "N", out _) == false) {
            return null;
        }
        return Path.Combine(fullRoot, components[0]);
    }

    [SetUp]
    public void SetUp() {
        Transport = new();
        PreparedPath = null;
    }

    [TearDown]
    public void TearDown() {
        if (PreparedPath is not null) {
            var stagingRoot = GetStagingRoot(PreparedPath);
            if (stagingRoot is not null && Directory.Exists(stagingRoot)) {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public async Task Work_MaterializesStreamSourcesForTheClipboardAndPasteIO(bool moveOnPaste) {
        var source = new TestStreamSource("readme.txt", "archive contents");
        var set = new ClipboardSetIO(Transport);
        var setData = new TestClipboardSetIOData {
            MoveOnPaste = moveOnPaste
        };
        var setWorked = await set.Work(new[] {
            new ProvidedIO {
                StreamSets = new[] { new TestStreamSet(new[] { source }) }
            }
        }, setData, null, CancellationToken.None);

        Assert.That(setWorked, Is.True);
        Assert.That(Transport.Files.Count, Is.EqualTo(1));
        PreparedPath = Transport.Files[0];
        var stagingRoot = GetStagingRoot(PreparedPath);
        Assert.That(stagingRoot, Is.Not.Null);
        Assert.That(Directory.Exists(stagingRoot), Is.True);
        Assert.That(File.Exists(PreparedPath), Is.True);
        Assert.That(File.ReadAllText(PreparedPath), Is.EqualTo("archive contents"));
        Assert.That(source.ReadyDisposed, Is.True);
        Assert.That(source.Stream.CanRead, Is.False);

        var pasted = new List<IProvidedIO>();
        var getData = new TestClipboardGetIOData {
            MoveOnPaste = moveOnPaste
        };
        var getWorked = await new ClipboardGetIO(Transport).Work(
            pasted,
            getData,
            null,
            CancellationToken.None);

        Assert.That(getWorked, Is.True);
        Assert.That(pasted.FileSources(), Is.EqualTo(new[] { PreparedPath }));
        Assert.That(File.Exists(PreparedPath), Is.True);
        Assert.That(File.ReadAllText(PreparedPath), Is.EqualTo("archive contents"));
        Assert.That(getData.MoveOnPaste, Is.EqualTo(moveOnPaste));
        Assert.That(Transport.ClearCount, Is.EqualTo(moveOnPaste ? 1 : 0));
    }

    [Test]
    public async Task Work_StagesDirectoryMarkerAtTheSourceRelativePath() {
        var source = new TestStreamSource(
            "archive-folder",
            string.Empty,
            sourceDirectory: Path.GetTempPath());
        var set = new ClipboardSetIO(Transport);
        var worked = await set.Work(new[] {
            new ProvidedIO {
                StreamSets = new[] { new TestStreamSet(new[] { source }) }
            }
        }, new TestClipboardSetIOData(), null, CancellationToken.None);

        Assert.That(worked, Is.True);
        Assert.That(Transport.Files.Count, Is.EqualTo(1));
        PreparedPath = Transport.Files[0];
        Assert.That(Path.GetFileName(PreparedPath), Is.EqualTo("archive-folder"));
        Assert.That(Path.GetFullPath(PreparedPath), Is.Not.EqualTo(Path.GetFullPath(Path.GetTempPath())));
        Assert.That(Directory.Exists(PreparedPath), Is.True);
    }

    [Test]
    public async Task Work_PreservesDirectAndVirtualSetsInOneProvidedIO() {
        var directFile = Path.Combine(Path.GetTempPath(), $"clipboard-direct-{Guid.NewGuid():N}.txt");
        File.WriteAllText(directFile, "direct contents");
        try {
            var virtualSource = new TestStreamSource("virtual.txt", "virtual contents");
            var directSet = new TestStreamSet(
                Array.Empty<TestStreamSource>(),
                failure: new IOException("The direct set should not enumerate streams."),
                directFiles: new[] { directFile });
            var virtualSet = new TestStreamSet(new[] { virtualSource });
            var set = new ClipboardSetIO(Transport);
            var worked = await set.Work(new[] {
                new ProvidedIO {
                    StreamSets = new[] { directSet, virtualSet }
                }
            }, new TestClipboardSetIOData(), null, CancellationToken.None);

            Assert.That(worked, Is.True);
            Assert.That(Transport.Files.Count, Is.EqualTo(2));
            Assert.That(Transport.Files.Contains(Path.GetFullPath(directFile)), Is.True);
            PreparedPath = Transport.Files.Cast<string>()
                .Single(path => string.Equals(path, Path.GetFullPath(directFile), StringComparison.Ordinal) == false);
            Assert.That(File.ReadAllText(PreparedPath), Is.EqualTo("virtual contents"));
        }
        finally {
            File.Delete(directFile);
        }
    }

    [Test]
    public async Task Work_PreservesTheCurrentClipboardWhenStagedPathsDifferOnlyByCase() {
        var externalFile = Path.Combine(Path.GetTempPath(), "clipboard-external.txt");
        Transport.Files.Add(externalFile);
        Transport.OtherFormats["unrelated"] = new object();
        Transport.HasFileDropList = true;
        Transport.PreferredDropEffect = DragDropEffects.Move;
        var sources = new[] {
            new TestStreamSource("Foo/a.txt", "first"),
            new TestStreamSource("foo/b.txt", "second")
        };
        var set = new ClipboardSetIO(Transport);
        var worked = await set.Work(new[] {
            new ProvidedIO {
                StreamSets = new[] { new TestStreamSet(sources) }
            }
        }, new TestClipboardSetIOData(), null, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(worked, Is.False);
            Assert.That(Transport.Files.Cast<string>(), Is.EqualTo(new[] { externalFile }));
            Assert.That(Transport.OtherFormats.ContainsKey("unrelated"), Is.True);
            Assert.That(Transport.PreferredDropEffect, Is.EqualTo(DragDropEffects.Move));
            Assert.That(Transport.ClearCount, Is.EqualTo(0));
            Assert.That(Transport.PublishFileDropListCount, Is.EqualTo(0));
        }
    }

    [Test]
    public async Task Work_PreservesTheCurrentClipboardWhenStreamPreparationFails() {
        var externalFile = Path.Combine(Path.GetTempPath(), "clipboard-external.txt");
        Transport.Files.Add(externalFile);
        Transport.OtherFormats["unrelated"] = new object();
        Transport.HasFileDropList = true;
        Transport.PreferredDropEffect = DragDropEffects.Move;
        var firstSource = new TestStreamSource("readme.txt", "archive contents");
        var source = new TestStreamSource("failed.txt", "", new IOException("Preparation failed."));
        var set = new ClipboardSetIO(Transport);
        var succeeded = false;
        try {
            succeeded = await set.Work(new[] {
                new ProvidedIO {
                    StreamSets = new[] { new TestStreamSet(new[] { firstSource, source }) }
                }
            }, new TestClipboardSetIOData(), null, CancellationToken.None);
        }
        catch (IOException) {
        }

        using (Assert.EnterMultipleScope()) {
            Assert.That(succeeded, Is.False);
            Assert.That(Transport.Files.Cast<string>(), Is.EqualTo(new[] { externalFile }));
            Assert.That(Transport.OtherFormats.ContainsKey("unrelated"), Is.True);
            Assert.That(Transport.PreferredDropEffect, Is.EqualTo(DragDropEffects.Move));
            Assert.That(Transport.ClearCount, Is.EqualTo(0));
            Assert.That(Transport.PublishFileDropListCount, Is.EqualTo(0));
        }
    }

    [Test]
    public async Task Work_PreservesTheCurrentClipboardWhenCanceledAfterStaging() {
        var externalFile = Path.Combine(Path.GetTempPath(), "clipboard-external.txt");
        Transport.Files.Add(externalFile);
        Transport.OtherFormats["unrelated"] = new object();
        Transport.HasFileDropList = true;
        Transport.PreferredDropEffect = DragDropEffects.Move;
        var source = new TestStreamSource("readme.txt", "archive contents");
        using var tokenSource = new CancellationTokenSource();
        var streamSet = new TestStreamSet(new[] { source }, afterSources: tokenSource.Cancel);
        var set = new ClipboardSetIO(Transport);

        await Assert.ThrowsAsync<OperationCanceledException>(async () => {
            await set.Work(new[] {
                new ProvidedIO {
                    StreamSets = new[] { streamSet }
                }
            }, new TestClipboardSetIOData(), null, tokenSource.Token);
        });

        using (Assert.EnterMultipleScope()) {
            Assert.That(source.ReadyDisposed, Is.True);
            Assert.That(Transport.Files.Cast<string>(), Is.EqualTo(new[] { externalFile }));
            Assert.That(Transport.OtherFormats.ContainsKey("unrelated"), Is.True);
            Assert.That(Transport.PreferredDropEffect, Is.EqualTo(DragDropEffects.Move));
            Assert.That(Transport.ClearCount, Is.EqualTo(0));
            Assert.That(Transport.PublishFileDropListCount, Is.EqualTo(0));
        }
    }

    [Test]
    public async Task Work_PreservesTheCurrentClipboardWhenStreamPathEscapesStaging() {
        var externalFile = Path.Combine(Path.GetTempPath(), "clipboard-external.txt");
        Transport.Files.Add(externalFile);
        Transport.OtherFormats["unrelated"] = new object();
        Transport.HasFileDropList = true;
        Transport.PreferredDropEffect = DragDropEffects.Move;
        var source = new TestStreamSource("../outside.txt", "archive contents");
        var set = new ClipboardSetIO(Transport);
        var succeeded = await set.Work(new[] {
            new ProvidedIO {
                StreamSets = new[] { new TestStreamSet(new[] { source }) }
            }
        }, new TestClipboardSetIOData(), null, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(succeeded, Is.False);
            Assert.That(source.Stream, Is.Null);
            Assert.That(Transport.Files.Cast<string>(), Is.EqualTo(new[] { externalFile }));
            Assert.That(Transport.OtherFormats.ContainsKey("unrelated"), Is.True);
            Assert.That(Transport.PreferredDropEffect, Is.EqualTo(DragDropEffects.Move));
            Assert.That(Transport.ClearCount, Is.EqualTo(0));
            Assert.That(Transport.PublishFileDropListCount, Is.EqualTo(0));
        }
    }
}
