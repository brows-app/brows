using Brows.Operations;
using Brows.SevenZip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Tests the library that talks to the 7-Zip engine. The tests use real
/// archives that are created and removed on the real file system.
/// </summary>
[TestFixture]
internal sealed class SevenZipArchiveTest {
    private SevenZipTestSandbox Sandbox;

    [SetUp]
    public void SetUp() {
        Sandbox = new();
    }

    [TearDown]
    public void TearDown() {
        Sandbox.Dispose();
    }

    /// <summary>
    /// Creates an archive that holds the given files.
    /// </summary>
    private string CreateArchive(string name, params (string Path, string Contents)[] files) {
        return CreateArchiveWithCompression(name, null, files);
    }

    /*
     * Creates an archive that holds the given files with the given compression level.
     */
    private string CreateArchiveWithCompression(string name,
                                                int? compressionLevel,
                                                params (string Path, string Contents)[] files) {
        var items = new List<SevenZipUpdateItem>();
        foreach (var (path, contents) in files) {
            var source = Sandbox.WriteFile(path, contents);
            var info = new FileInfo(source);
            items.Add(new SevenZipUpdateItem(
                path: path,
                sourcePath: source,
                isDirectory: false,
                size: info.Length,
                modifiedTime: null,
                attributes: 0));
        }
        var archivePath = Sandbox.PathFor(name);
        SevenZipArchive.Create(archivePath, items, compressionLevel);
        return archivePath;
    }

    /*
     * Changes the first packed byte without changing the archive headers.
     */
    private static void CorruptPackedPayload(string archivePath) {
        const int packedPayloadOffset = 32;
        var bytes = File.ReadAllBytes(archivePath);
        Assert.That(bytes.Length, Is.GreaterThan(packedPayloadOffset));
        bytes[packedPayloadOffset] ^= 1;
        File.WriteAllBytes(archivePath, bytes);
    }

    [Test]
    public void Create_WritesTheArchiveToTheGivenPath() {
        var archivePath = CreateArchive("test.7z", ("hello.txt", "hello"));
        Assert.That(File.Exists(archivePath), Is.True);
        Assert.That(new FileInfo(archivePath).Length, Is.GreaterThan(0));
    }

    [Test]
    public void Open_ReadsTheItemsOfTheArchive() {
        var archivePath = CreateArchive("test.7z", ("first.txt", "first"), ("second.txt", "second"));
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(archive.Entries.Select(entry => entry.Path),
                Is.EquivalentTo(new[] { "first.txt", "second.txt" }));
        }
    }

    [Test]
    public void Create_UsesTheCompressionLevelThatIsGiven() {
        /*
         * The content of the file is easy to compress, so the archive that
         * stores the file without compressing it is much larger than the
         * archive that compresses it.
         */
        var source = Sandbox.WriteFile("big.txt", new string('a', 100000));
        var info = new FileInfo(source);
        var item = new SevenZipUpdateItem("big.txt", source, false, info.Length, null, 0);
        var stored = Sandbox.PathFor("stored.7z");
        var compressed = Sandbox.PathFor("compressed.7z");
        SevenZipArchive.Create(stored, new[] { item }, 0);
        SevenZipArchive.Create(compressed, new[] { item }, 9);
        using (var archive = SevenZipArchive.Open(stored)) {
            Assert.That(archive.Entries.Single().PackedSize, Is.GreaterThan(1000));
        }
        using (var archive = SevenZipArchive.Open(compressed)) {
            Assert.That(archive.Entries.Single().PackedSize, Is.LessThan(1000));
        }
    }

    [Test]
    public void Open_ReadsTheSizeOfTheItems() {
        var archivePath = CreateArchive("test.7z", ("hello.txt", "hello"));
        using (var archive = SevenZipArchive.Open(archivePath)) {
            var entry = archive.Entries.Single();
            Assert.That(entry.Size, Is.EqualTo(5));
        }
    }

    [Test]
    public void Open_ReadsTheNamesOfTheItemsWithForwardSlashes() {
        var source = Sandbox.WriteFile("hello.txt", "hello");
        var info = new FileInfo(source);
        var item = new SevenZipUpdateItem("folder\\hello.txt", source, false, info.Length, null, 0);
        var archivePath = Sandbox.PathFor("test.7z");
        SevenZipArchive.Create(archivePath, new[] { item }, null);
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(archive.Entries.Single().Path, Is.EqualTo("folder/hello.txt"));
        }
    }

    [Test]
    public void Open_ReadsTheItemsThatAreInAFolder() {
        var archivePath = CreateArchive("test.7z", ("folder/hello.txt", "hello"));
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(archive.Entries.Single().Path, Is.EqualTo("folder/hello.txt"));
        }
    }

    [Test]
    public void Extract_WritesTheContentOfTheItem() {
        var archivePath = CreateArchive("test.7z", ("hello.txt", "hello"));
        var destination = Sandbox.PathFor("extracted.txt");
        using (var archive = SevenZipArchive.Open(archivePath)) {
            archive.Extract(0, destination, overwrite: false);
        }
        Assert.That(File.ReadAllText(destination), Is.EqualTo("hello"));
    }

    [Test]
    public void Extract_ThrowsWhenTheFileExistsAndOverwriteIsFalse() {
        var archivePath = CreateArchive("test.7z", ("hello.txt", "hello"));
        var destination = Sandbox.WriteFile("extracted.txt", "old");
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(() => archive.Extract(0, destination, overwrite: false),
                Throws.InstanceOf<SevenZipException>());
        }
        Assert.That(File.ReadAllText(destination), Is.EqualTo("old"));
    }

    [Test]
    public void Extract_OverwritesTheFileWhenOverwriteIsTrue() {
        var archivePath = CreateArchive("test.7z", ("hello.txt", "hello"));
        var destination = Sandbox.WriteFile("extracted.txt", "old");
        using (var archive = SevenZipArchive.Open(archivePath)) {
            archive.Extract(0, destination, overwrite: true);
        }
        Assert.That(File.ReadAllText(destination), Is.EqualTo("hello"));
    }

    [Test]
    public void ExtractAll_WritesEveryItemToTheFolder() {
        var archivePath = CreateArchive("test.7z", ("first.txt", "first"), ("folder/second.txt", "second"));
        var directory = Sandbox.CreateDirectory("extracted");
        using (var archive = SevenZipArchive.Open(archivePath)) {
            archive.ExtractAll(directory, overwrite: false);
        }
        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(Path.Combine(directory, "first.txt")), Is.EqualTo("first"));
            Assert.That(File.ReadAllText(Path.Combine(directory, "folder", "second.txt")), Is.EqualTo("second"));
        }
    }

    [Test]
    public void Extract_ThrowsWhenTheStoredPayloadHasABadCrc() {
        var archivePath = CreateArchiveWithCompression("test.7z", 0, ("hello.txt", new string('x', 4096)));
        CorruptPackedPayload(archivePath);
        var destination = Sandbox.PathFor("extracted.txt");
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(() => archive.Extract(0, destination, overwrite: false),
                Throws.InstanceOf<SevenZipException>());
        }
        Assert.That(File.Exists(destination), Is.False);
    }

    [Test]
    public void Extract_ThrowsAndPreservesExistingTargetsWhenStoredPayloadHasABadCrc() {
        var archivePath = CreateArchiveWithCompression("test.7z", 0, ("hello.txt", new string('x', 4096)));
        CorruptPackedPayload(archivePath);
        var destination = Sandbox.WriteFile("output/hello.txt", "existing target");
        var unrelated = Sandbox.WriteFile("output/unrelated.txt", "unrelated sentinel");
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(() => archive.Extract(0, destination, overwrite: true),
                Throws.InstanceOf<SevenZipException>());
        }
        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(destination), Is.EqualTo("existing target"));
            Assert.That(File.ReadAllText(unrelated), Is.EqualTo("unrelated sentinel"));
        }
    }

    [Test]
    public void ExtractAll_ThrowsWhenTheStoredPayloadHasABadCrc() {
        var archivePath = CreateArchiveWithCompression("test.7z", 0, ("hello.txt", new string('x', 4096)));
        CorruptPackedPayload(archivePath);
        var directory = Sandbox.CreateDirectory("extracted");
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(() => archive.ExtractAll(directory, overwrite: false),
                Throws.InstanceOf<SevenZipException>());
        }
        Assert.That(File.Exists(Path.Combine(directory, "hello.txt")), Is.False);
    }

    [Test]
    public void ExtractAll_ThrowsAndPreservesExistingTargetsWhenStoredPayloadHasABadCrc() {
        var archivePath = CreateArchiveWithCompression("test.7z", 0, ("hello.txt", new string('x', 4096)));
        CorruptPackedPayload(archivePath);
        var directory = Sandbox.CreateDirectory("extracted");
        var destination = Sandbox.WriteFile("extracted/hello.txt", "existing target");
        var unrelated = Sandbox.WriteFile("extracted/unrelated.txt", "unrelated sentinel");
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(() => archive.ExtractAll(directory, overwrite: true),
                Throws.InstanceOf<SevenZipException>());
        }
        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(destination), Is.EqualTo("existing target"));
            Assert.That(File.ReadAllText(unrelated), Is.EqualTo("unrelated sentinel"));
        }
    }

    [Test]
    public void ExtractAll_ThrowsWhenSolidCompressedPayloadIsCorrupted() {
        var archivePath = CreateArchiveWithCompression(
            "solid.7z",
            5,
            ("first.txt", new string('x', 100000)),
            ("second.txt", new string('x', 100000)));
        CorruptPackedPayload(archivePath);
        var directory = Sandbox.PathFor("extracted");
        var preserved = Sandbox.WriteFile("extracted/preserved.txt", "keep");
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(() => archive.ExtractAll(directory, overwrite: false),
                Throws.InstanceOf<SevenZipException>());
        }
        using (Assert.EnterMultipleScope()) {
            Assert.That(File.Exists(Path.Combine(directory, "first.txt")), Is.False);
            Assert.That(File.Exists(Path.Combine(directory, "second.txt")), Is.False);
            Assert.That(File.ReadAllText(preserved), Is.EqualTo("keep"));
        }
    }

    [Test]
    public void ExtractAll_DoesNotWriteTheItemsOutsideOfTheFolderThatIsGiven() {
        /*
         * An archive may hold an item whose name points outside of the folder
         * that the archive is extracted to. Such an item must never be
         * written outside of that folder.
         */
        var source = Sandbox.WriteFile("source/evil.txt", "evil");
        var info = new FileInfo(source);
        var item = new SevenZipUpdateItem("../evil.txt", source, false, info.Length, null, 0);
        var archivePath = Sandbox.PathFor("test.7z");
        SevenZipArchive.Create(archivePath, new[] { item }, null);
        var directory = Sandbox.CreateDirectory("extracted");
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(() => archive.ExtractAll(directory, overwrite: false),
                Throws.InstanceOf<SevenZipException>());
        }
        Assert.That(File.Exists(Sandbox.PathFor("evil.txt")), Is.False);
    }

    [Test]
    public void IndexOf_FindsTheItemOfTheGivenPath() {
        var archivePath = CreateArchive("test.7z", ("first.txt", "first"), ("folder/second.txt", "second"));
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(archive.IndexOf("folder/second.txt"), Is.EqualTo(1));
        }
    }

    [Test]
    public void IndexOf_IsMinusOneForAnItemThatIsNotInTheArchive() {
        var archivePath = CreateArchive("test.7z", ("first.txt", "first"));
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(archive.IndexOf("missing.txt"), Is.EqualTo(-1));
        }
    }

    [Test]
    public void Update_AddsTheItemsToTheArchive() {
        var archivePath = CreateArchive("test.7z", ("first.txt", "first"));
        var source = Sandbox.WriteFile("second.txt", "second");
        var info = new FileInfo(source);
        var item = new SevenZipUpdateItem("second.txt", source, false, info.Length, null, 0);
        SevenZipArchive.Update(archivePath, new uint[] { 0 }, new[] { item }, null);
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(archive.Entries.Select(entry => entry.Path),
                Is.EquivalentTo(new[] { "first.txt", "second.txt" }));
        }
    }

    [Test]
    public void Update_RemovesTheItemsThatAreNotKept() {
        var archivePath = CreateArchive("test.7z", ("first.txt", "first"));
        var source = Sandbox.WriteFile("second.txt", "second");
        var info = new FileInfo(source);
        var item = new SevenZipUpdateItem("second.txt", source, false, info.Length, null, 0);
        SevenZipArchive.Update(archivePath, Array.Empty<uint>(), new[] { item }, null);
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(archive.Entries.Select(entry => entry.Path),
                Is.EquivalentTo(new[] { "second.txt" }));
        }
    }

    [Test]
    public void Update_KeepsTheItemsThatAreNotLeftOut() {
        var archivePath = CreateArchive("test.7z", ("first.txt", "first"), ("second.txt", "second"));
        var source = Sandbox.WriteFile("third.txt", "third");
        var info = new FileInfo(source);
        var item = new SevenZipUpdateItem("third.txt", source, false, info.Length, null, 0);
        SevenZipArchive.Update(archivePath, new uint[] { 0 }, new[] { item }, null);
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(archive.Entries.Select(entry => entry.Path),
                Is.EquivalentTo(new[] { "first.txt", "third.txt" }));
        }
    }

    [Test]
    public void Update_KeepsTheContentOfTheItemsThatAreKept() {
        var archivePath = CreateArchive("test.7z", ("first.txt", "first"), ("second.txt", "second"));
        var source = Sandbox.WriteFile("third.txt", "third");
        var info = new FileInfo(source);
        var item = new SevenZipUpdateItem("third.txt", source, false, info.Length, null, 0);
        SevenZipArchive.Update(archivePath, new uint[] { 0 }, new[] { item }, null);
        var directory = Sandbox.CreateDirectory("extracted");
        using (var archive = SevenZipArchive.Open(archivePath)) {
            archive.ExtractAll(directory, overwrite: false);
        }
        Assert.That(File.ReadAllText(Path.Combine(directory, "first.txt")), Is.EqualTo("first"));
    }

    [Test]
    public void Open_ThrowsForAnArchiveThatDoesNotExist() {
        Assert.That(() => SevenZipArchive.Open(Sandbox.PathFor("missing.7z")),
            Throws.InstanceOf<SevenZipException>());
    }

    [Test]
    public void Open_ThrowsForAnInvalidSignature() {
        var archivePath = Sandbox.WriteFile("invalid.7z", "This is not a 7-Zip archive.");
        Assert.That(() => {
            using (SevenZipArchive.Open(archivePath)) {
            }
        }, Throws.InstanceOf<SevenZipException>());
    }

    [Test]
    public void Open_ThrowsForATruncatedArchive() {
        var archivePath = CreateArchive("truncated.7z", ("hello.txt", "hello"));
        var bytes = File.ReadAllBytes(archivePath);
        Assert.That(bytes.Length, Is.GreaterThan(1));
        File.WriteAllBytes(archivePath, bytes[..^1]);
        Assert.That(() => {
            using (SevenZipArchive.Open(archivePath)) {
            }
        }, Throws.InstanceOf<SevenZipException>());
    }

    [Test]
    public void Open_OpensAValidEmptyArchive() {
        var archivePath = Sandbox.PathFor("empty.7z");
        SevenZipArchive.Create(archivePath, Array.Empty<SevenZipUpdateItem>(), null);
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(archive.Entries, Is.Empty);
        }
    }

    [Test]
    public async Task Create_AddsItemsToAValidEmptyArchive() {
        var archivePath = Sandbox.PathFor("empty.7z");
        SevenZipArchive.Create(archivePath, Array.Empty<SevenZipUpdateItem>(), null);
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(archive.Entries, Is.Empty);
        }
        var source = Sandbox.WriteFile("hello.txt", "hello");
        var files = new Dictionary<string, string> {
            { "hello.txt", source }
        };
        await SevenZipArchiveFile.Create(archivePath, files, null, null, CancellationToken.None);
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(archive.Entries.Select(entry => entry.Path), Is.EqualTo(new[] { "hello.txt" }));
        }
    }

    [Test]
    public async Task Create_LeavesAnInvalidExistingFileUntouched() {
        var archivePath = Sandbox.WriteFile("invalid.7z", "This is not a 7-Zip archive.");
        var original = File.ReadAllBytes(archivePath);
        var source = Sandbox.WriteFile("hello.txt", "hello");
        var files = new Dictionary<string, string> {
            { "hello.txt", source }
        };
        await Assert.ThrowsAsync<SevenZipException>(() =>
            SevenZipArchiveFile.Create(archivePath, files, null, null, CancellationToken.None));
        Assert.That(File.ReadAllBytes(archivePath), Is.EqualTo(original));
    }

    [Test]
    public void Update_ThrowsAndPreservesAnInvalidExistingFile() {
        var archivePath = Sandbox.WriteFile("invalid.7z", "This is not a 7-Zip archive.");
        var original = File.ReadAllBytes(archivePath);
        var source = Sandbox.WriteFile("hello.txt", "hello");
        var sourceInfo = new FileInfo(source);
        var item = new SevenZipUpdateItem(
            path: "hello.txt",
            sourcePath: source,
            isDirectory: false,
            size: sourceInfo.Length,
            modifiedTime: null,
            attributes: 0);
        Assert.That(() => SevenZipArchive.Update(
            archivePath,
            Array.Empty<uint>(),
            new[] { item },
            null), Throws.InstanceOf<SevenZipException>());
        Assert.That(File.ReadAllBytes(archivePath), Is.EqualTo(original));
    }

    [Test]
    public async Task Update_RejectsAnInvalidFileCreatedAfterTheArchivePathWasAcquired() {
        var archivePath = Sandbox.PathFor("invalid.7z");
        var path = SevenZipArchivePath.GetForTest(new FileInfo(archivePath));
        var original = new byte[] { 0x42, 0x72, 0x6F, 0x77, 0x73 };
        try {
            File.WriteAllBytes(archivePath, original);
            var source = Sandbox.WriteFile("hello.txt", "hello");
            var info = new SevenZipArchiveUpdate {
                CreateEntriesFromFiles = new Dictionary<string, string> {
                    { "hello.txt", source }
                }
            };
            await Assert.ThrowsAsync<SevenZipException>(() =>
                path.Update(info, null, CancellationToken.None));
            Assert.That(File.ReadAllBytes(archivePath), Is.EqualTo(original));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task ExtractAll_CancelsNativeWorkAndPreservesExistingOutputs() {
        var archivePath = Sandbox.PathFor("cancel.7z");
        var source = Sandbox.PathFor("payload.bin");
        var payload = new byte[8 * 1024 * 1024];
        Random.Shared.NextBytes(payload);
        File.WriteAllBytes(source, payload);
        var sourceInfo = new FileInfo(source);
        var item = new SevenZipUpdateItem(
            path: "payload.bin",
            sourcePath: source,
            isDirectory: false,
            size: sourceInfo.Length,
            modifiedTime: null,
            attributes: 0);
        SevenZipArchive.Create(archivePath, new[] { item }, compressionLevel: 0);

        var directory = Sandbox.CreateDirectory("output");
        var destination = Sandbox.WriteFile("output/payload.bin", "preserve this file");
        var path = SevenZipArchivePath.GetForTest(new FileInfo(archivePath));
        var progress = new NativeProgressGate();
        using (var cancellation = new CancellationTokenSource()) {
            var extraction = path.ExtractAll(
                directory,
                overwrite: true,
                progress: progress,
                token: cancellation.Token);
            try {
                await progress.NativeProgress.Task.WaitAsync(TimeSpan.FromSeconds(5));
                cancellation.Cancel();
                progress.Release();
                await Assert.ThrowsAsync<OperationCanceledException>(() => extraction);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(progress.BytesReported, Is.GreaterThan(0));
                    Assert.That(File.ReadAllText(destination), Is.EqualTo("preserve this file"));
                    Assert.That(Directory.EnumerateFiles(directory, "b7-*.tmp"), Is.Empty);
                }
            }
            finally {
                progress.Release();
                if (extraction.IsCompleted == false) {
                    await extraction;
                }
                path.Release();
            }
        }
    }

    [Test]
    public async Task Create_CancelsNativeWorkAndDoesNotPublishAnArchive() {
        var archivePath = Sandbox.PathFor("cancel-create.7z");
        var source = Sandbox.PathFor("create-payload.bin");
        var payload = new byte[8 * 1024 * 1024];
        Random.Shared.NextBytes(payload);
        File.WriteAllBytes(source, payload);
        var sourceInfo = new FileInfo(source);
        var item = new SevenZipUpdateItem(
            path: "payload.bin",
            sourcePath: source,
            isDirectory: false,
            size: sourceInfo.Length,
            modifiedTime: null,
            attributes: 0);
        var progress = new NativeProgressGate();
        using (var cancellation = new CancellationTokenSource()) {
            var creation = Task.Run(() => SevenZipArchive.Create(
                archivePath,
                new[] { item },
                compressionLevel: 0,
                progress: progress,
                token: cancellation.Token));
            try {
                await progress.NativeProgress.Task.WaitAsync(TimeSpan.FromSeconds(5));
                cancellation.Cancel();
                progress.Release();
                await Assert.ThrowsAsync<OperationCanceledException>(() => creation);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(progress.BytesReported, Is.GreaterThan(0));
                    Assert.That(File.Exists(archivePath), Is.False);
                    Assert.That(Directory.EnumerateFiles(Sandbox.Root, "b7-*.tmp"), Is.Empty);
                }
            }
            finally {
                progress.Release();
                if (creation.IsCompleted == false) {
                    try {
                        await creation;
                    }
                    catch (OperationCanceledException) {
                    }
                }
            }
        }
    }

    [Test]
    public async Task Update_CancelsNativeWorkAndPreservesTheExistingArchive() {
        var originalSource = Sandbox.WriteFile("original.txt", "original archive contents");
        var originalInfo = new FileInfo(originalSource);
        var originalItem = new SevenZipUpdateItem(
            path: "original.txt",
            sourcePath: originalSource,
            isDirectory: false,
            size: originalInfo.Length,
            modifiedTime: null,
            attributes: 0);
        var archivePath = Sandbox.PathFor("cancel-update.7z");
        SevenZipArchive.Create(archivePath, new[] { originalItem }, compressionLevel: 0);
        var originalArchive = File.ReadAllBytes(archivePath);

        var source = Sandbox.PathFor("update-payload.bin");
        var payload = new byte[8 * 1024 * 1024];
        Random.Shared.NextBytes(payload);
        File.WriteAllBytes(source, payload);
        var sourceInfo = new FileInfo(source);
        var item = new SevenZipUpdateItem(
            path: "payload.bin",
            sourcePath: source,
            isDirectory: false,
            size: sourceInfo.Length,
            modifiedTime: null,
            attributes: 0);
        var progress = new NativeProgressGate();
        using (var cancellation = new CancellationTokenSource()) {
            var update = Task.Run(() => SevenZipArchive.Update(
                archivePath,
                new uint[] { 0 },
                new[] { item },
                compressionLevel: 0,
                progress: progress,
                token: cancellation.Token));
            try {
                await progress.NativeProgress.Task.WaitAsync(TimeSpan.FromSeconds(5));
                cancellation.Cancel();
                progress.Release();
                await Assert.ThrowsAsync<OperationCanceledException>(() => update);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(progress.BytesReported, Is.GreaterThan(0));
                    Assert.That(File.ReadAllBytes(archivePath), Is.EqualTo(originalArchive));
                    Assert.That(Directory.EnumerateFiles(Sandbox.Root, "b7-*.tmp"), Is.Empty);
                }
            }
            finally {
                progress.Release();
                if (update.IsCompleted == false) {
                    try {
                        await update;
                    }
                    catch (OperationCanceledException) {
                    }
                }
            }
        }
    }

    [Test]
    public async Task ExtractAll_ProgressExceptionReturnsToManagedCodeAndPreservesExistingOutputs() {
        var archivePath = CreateArchiveWithCompression(
            "callback-error.7z",
            0,
            ("payload.bin", new string('x', 1024 * 1024)));
        var directory = Sandbox.CreateDirectory("callback-error-output");
        var destination = Sandbox.WriteFile("callback-error-output/payload.bin", "preserve this file");
        var expected = new InvalidOperationException("Injected managed progress failure.");
        var progress = new NativeProgressGate(expected);
        using var archive = SevenZipArchive.Open(archivePath);

        var extraction = Task.Run(() => archive.ExtractAll(
            directory,
            overwrite: true,
            progress: progress,
            token: CancellationToken.None));
        try {
            await progress.NativeProgress.Task.WaitAsync(TimeSpan.FromSeconds(5));
            progress.Release();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => extraction);

            using (Assert.EnterMultipleScope()) {
                Assert.That(error, Is.SameAs(expected));
                Assert.That(File.ReadAllText(destination), Is.EqualTo("preserve this file"));
                Assert.That(Directory.EnumerateFiles(directory, "b7-*.tmp"), Is.Empty);
            }
        }
        finally {
            progress.Release();
            if (extraction.IsCompleted == false) {
                try {
                    await extraction;
                }
                catch (InvalidOperationException) {
                }
            }
        }
    }

    [Test]
    public void Open_EntryReadThrows_ClosesTheNativeOwnerOnceIncludingFinalization() {
        var closed = 0;
        var closedHandle = IntPtr.Zero;
        Action<IntPtr> closeNative = handle => {
            closedHandle = handle;
            Interlocked.Increment(ref closed);
        };

        Assert.That(() => OpenArchiveWithEntryReadFailure(closeNative),
            Throws.TypeOf<InvalidOperationException>());
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        using (Assert.EnterMultipleScope()) {
            Assert.That(closed, Is.EqualTo(1));
            Assert.That(closedHandle, Is.EqualTo(new IntPtr(1)));
        }
    }

    [Test]
    public void Create_NativeAllocationFault_CleansTheStagedOutput() {
        var source = Sandbox.WriteFile("fault-create.txt", "create output");
        var sourceInfo = new FileInfo(source);
        var item = new SevenZipUpdateItem("fault-create.txt", source, false, sourceInfo.Length, null, 0);
        var archivePath = Sandbox.PathFor("fault-create.7z");

        try {
            SevenZipArchive.SetNativeFaultForTesting(
                SevenZipArchive.NativeFaultPoint.AfterOutputStaged,
                SevenZipArchive.NativeFaultKind.BadAlloc);
            var error = Assert.Throws<SevenZipException>(() =>
                SevenZipArchive.Create(archivePath, new[] { item }, compressionLevel: 0));
            using (Assert.EnterMultipleScope()) {
                Assert.That(error.Error, Is.EqualTo(2117008));
                Assert.That(File.Exists(archivePath), Is.False);
                Assert.That(Directory.EnumerateFiles(Sandbox.Root, "b7-*.tmp"), Is.Empty);
            }
        }
        finally {
            SevenZipArchive.SetNativeFaultForTesting(
                SevenZipArchive.NativeFaultPoint.None,
                SevenZipArchive.NativeFaultKind.None);
        }
    }

    [Test]
    public void Open_NativeLengthFault_ReleasesTheArchiveAndStream() {
        var archivePath = CreateArchive("fault-open.7z", ("hello.txt", "hello"));
        try {
            SevenZipArchive.SetNativeFaultForTesting(
                SevenZipArchive.NativeFaultPoint.AfterArchiveCreated,
                SevenZipArchive.NativeFaultKind.LengthError);
            var error = Assert.Throws<SevenZipException>(() => SevenZipArchive.Open(archivePath));
            Assert.That(error.Error, Is.EqualTo(2117008));
        }
        finally {
            SevenZipArchive.SetNativeFaultForTesting(
                SevenZipArchive.NativeFaultPoint.None,
                SevenZipArchive.NativeFaultKind.None);
        }

        File.Delete(archivePath);
        Assert.That(File.Exists(archivePath), Is.False);
    }

    [Test]
    public void Update_NativeCallbackFault_PreservesTheExistingArchiveAndCleansTheStage() {
        var archivePath = CreateArchive("fault-update.7z", ("first.txt", "first"));
        var originalArchive = File.ReadAllBytes(archivePath);
        var source = Sandbox.WriteFile("fault-update.txt", "replacement contents");
        var sourceInfo = new FileInfo(source);
        var item = new SevenZipUpdateItem("second.txt", source, false, sourceInfo.Length, null, 0);

        try {
            SevenZipArchive.SetNativeFaultForTesting(
                SevenZipArchive.NativeFaultPoint.UpdateCallback,
                SevenZipArchive.NativeFaultKind.LengthError);
            var error = Assert.Throws<SevenZipException>(() =>
                SevenZipArchive.Update(archivePath, new uint[] { 0 }, new[] { item }, compressionLevel: 0));
            using (Assert.EnterMultipleScope()) {
                Assert.That(error.Error, Is.EqualTo(2117008));
                Assert.That(File.ReadAllBytes(archivePath), Is.EqualTo(originalArchive));
                Assert.That(Directory.EnumerateFiles(Sandbox.Root, "b7-*.tmp"), Is.Empty);
            }
        }
        finally {
            SevenZipArchive.SetNativeFaultForTesting(
                SevenZipArchive.NativeFaultPoint.None,
                SevenZipArchive.NativeFaultKind.None);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void OpenArchiveWithEntryReadFailure(Action<IntPtr> closeNative) {
        SevenZipArchive.OpenForTesting(
            new IntPtr(1),
            archive => {
                archive.GetHandle();
                throw new InvalidOperationException("Injected entry-load failure.");
            },
            closeNative);
    }

    private sealed class NativeProgressGate : IOperationProgress {
        private readonly ManualResetEventSlim ReleaseEvent = new();
        private readonly Exception Failure;
        private long _BytesReported;

        internal NativeProgressGate(Exception failure = null) {
            Failure = failure;
        }

        internal TaskCompletionSource NativeProgress { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal long BytesReported => Interlocked.Read(ref _BytesReported);

        internal void Release() {
            ReleaseEvent.Set();
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
            var progress = addProgress.GetValueOrDefault();
            if (progress <= 0) {
                return;
            }
            Interlocked.Add(ref _BytesReported, progress);
            NativeProgress.TrySetResult();
            if (ReleaseEvent.Wait(TimeSpan.FromSeconds(10)) == false) {
                throw new TimeoutException("The native progress gate was not released.");
            }
            if (Failure is not null) {
                throw Failure;
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
                await task(this, cancellationToken);
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
}
