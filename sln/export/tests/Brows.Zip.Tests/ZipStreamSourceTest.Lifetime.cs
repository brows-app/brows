using Brows.Entries;
using Brows.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed partial class ZipStreamSourceTest {
    private sealed class SourceContext : IDisposable {
        private bool Ended;

        public ZipEntry Entry { get; }
        public ZipProvider Provider { get; }
        public ZipArchivePath ArchivePath => Provider.Zip.ArchivePath;

        public SourceContext(ZipProvider provider, ZipEntry entry) {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        }

        public void End() {
            if (Ended == false) {
                ((IProvider)Provider).End();
                Ended = true;
            }
        }

        public void Dispose() {
            End();
            Provider.Dispose();
        }
    }

    private sealed class ReadBarrier {
        private readonly TaskCompletionSource<bool> BothSourcesReady = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private int Arrived;

        public Task Wait(CancellationToken token) {
            var count = Interlocked.Increment(ref Arrived);
            if (count == 2) {
                BothSourcesReady.TrySetResult(true);
            }
            return BothSourcesReady.Task.WaitAsync(token);
        }
    }

    private sealed class GatedSource : IEntryStreamSource {
        private readonly ZipStreamSource Source;
        private readonly ReadBarrier Barrier;

        public string EntryID => Source.EntryID;
        public string EntryName => Source.EntryName;
        public long StreamLength => Source.StreamLength;
        public string RelativePath => Source.RelativePath;
        public string SourceFile => Source.SourceFile;
        public string SourceDirectory => Source.SourceDirectory;
        public Stream Stream => new GatedReadStream(((IEntryStreamSource)Source).Stream, Barrier);

        public GatedSource(ZipStreamSource source, ReadBarrier barrier) {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Barrier = barrier ?? throw new ArgumentNullException(nameof(barrier));
        }

        public Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            return ((IEntryStreamSource)Source).StreamReady(token);
        }
    }

    private sealed class GatedReadStream : Stream {
        private readonly Stream Inner;
        private readonly ReadBarrier Barrier;

        private int Waited;

        public override bool CanRead => Inner.CanRead;
        public override bool CanSeek => Inner.CanSeek;
        public override bool CanWrite => Inner.CanWrite;
        public override long Length => Inner.Length;

        public override long Position {
            get => Inner.Position;
            set => Inner.Position = value;
        }

        public GatedReadStream(Stream inner, ReadBarrier barrier) {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Barrier = barrier ?? throw new ArgumentNullException(nameof(barrier));
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) {
            var shouldWait = Interlocked.Exchange(ref Waited, 1) == 0;
            if (shouldWait) {
                await Barrier.Wait(cancellationToken);
            }
            return await Inner.ReadAsync(buffer, cancellationToken);
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) {
            var shouldWait = Interlocked.Exchange(ref Waited, 1) == 0;
            if (shouldWait) {
                await Barrier.Wait(cancellationToken);
            }
            return await Inner.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count) {
            var shouldWait = Interlocked.Exchange(ref Waited, 1) == 0;
            if (shouldWait) {
                Barrier.Wait(CancellationToken.None).GetAwaiter().GetResult();
            }
            return Inner.Read(buffer, offset, count);
        }

        public override void Flush() {
            Inner.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin) {
            return Inner.Seek(offset, origin);
        }

        public override void SetLength(long value) {
            Inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count) {
            Inner.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                Inner.Dispose();
            }
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync() {
            return Inner.DisposeAsync();
        }
    }

    private static async Task<SourceContext> OpenSource(string id, string entryName) {
        var factory = new ZipProviderFactory();
        var zipID = await ZipID.Parse(id, factory, CancellationToken.None);
        if (zipID is null) {
            throw new InvalidOperationException("The test archive could not be opened.");
        }

        var provider = new ZipProvider(zipID, factory);
        try {
            var infos = await zipID.EntryInfo(CancellationToken.None);
            var info = infos.Single(item => item.Kind == ZipEntryKind.File && item.Name.Original == entryName);
            return new SourceContext(provider, new ZipEntry(provider, info));
        }
        catch {
            ((IProvider)provider).End();
            provider.Dispose();
            throw;
        }
    }

    private static async Task ReadSource(IEntryStreamSource source, CancellationToken token) {
        using var ready = await source.StreamReady(token);
        await using var stream = source.Stream;
        await stream.CopyToAsync(Stream.Null, token);
    }

    private static void WriteArchive(string fileName, params (string Name, byte[] Contents)[] entries) {
        using var file = new FileStream(fileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        foreach (var item in entries) {
            var entry = archive.CreateEntry(item.Name);
            using var stream = entry.Open();
            stream.Write(item.Contents);
        }
    }

    private static byte[] CreateArchiveBytes(params (string Name, byte[] Contents)[] entries) {
        using var file = new MemoryStream();
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true)) {
            foreach (var item in entries) {
                var entry = archive.CreateEntry(item.Name);
                using var stream = entry.Open();
                stream.Write(item.Contents);
            }
        }
        return file.ToArray();
    }

    private static byte[] Text(string value) {
        return Encoding.UTF8.GetBytes(value);
    }

    private static string ReadEntry(string fileName, string entryName) {
        using var file = File.OpenRead(fileName);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        var entry = archive.GetEntry(entryName)
            ?? throw new InvalidOperationException($"The archive entry was not found: {entryName}");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string ReadNestedEntry(string fileName, string archiveEntryName, string entryName) {
        using var file = File.OpenRead(fileName);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        var nestedEntry = archive.GetEntry(archiveEntryName)
            ?? throw new InvalidOperationException($"The nested archive entry was not found: {archiveEntryName}");
        using var nestedStream = nestedEntry.Open();
        using var nestedFile = new MemoryStream();
        nestedStream.CopyTo(nestedFile);
        nestedFile.Position = 0;
        using var nestedArchive = new ZipArchive(nestedFile, ZipArchiveMode.Read);
        var entry = nestedArchive.GetEntry(entryName)
            ?? throw new InvalidOperationException($"The archive entry was not found: {entryName}");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Test]
    public async Task Update_CopyWithinTheSameArchiveCompletes() {
        using var sandbox = new ZipTestSandbox();
        var archiveFileName = sandbox.PathFor("same.zip");
        WriteArchive(archiveFileName, ("payload.txt", Text("same archive payload")));
        using var source = await OpenSource(archiveFileName, "payload.txt");
        var target = ZipArchivePath.Get(new FileInfo(archiveFileName), new ZipArchiveNest(Array.Empty<string>()));
        try {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await Task.Run(() => target.Update(
                progress: null,
                token: timeout.Token,
                info: new ZipArchiveUpdate {
                    CopyStreamSets = new List<IEntryStreamSet> {
                        new ZipStreamSet(new IEntryStreamSource[] { source.Entry.Stream.Source })
                    }
                })).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(ReadEntry(archiveFileName, "payload.txt"), Is.EqualTo("same archive payload"));
        }
        finally {
            target.Release();
        }
    }

    [Test]
    public async Task Update_CopiesFromAnArchiveRootIntoItsNestedArchive() {
        using var sandbox = new ZipTestSandbox();
        var archiveFileName = sandbox.PathFor("root-to-nested.zip");
        WriteArchive(
            archiveFileName,
            ("payload.txt", Text("root source")),
            ("inner.zip", CreateArchiveBytes(("existing.txt", Text("existing")))));
        using var source = await OpenSource(archiveFileName, "payload.txt");
        var target = ZipArchivePath.Get(
            new FileInfo(archiveFileName),
            new ZipArchiveNest(new[] { "inner.zip" }));
        try {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await Task.Run(() => target.Update(
                progress: null,
                token: timeout.Token,
                info: new ZipArchiveUpdate {
                    CopyStreamSets = new List<IEntryStreamSet> {
                        new ZipStreamSet(new IEntryStreamSource[] { source.Entry.Stream.Source })
                    }
                })).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(ReadNestedEntry(archiveFileName, "inner.zip", "payload.txt"), Is.EqualTo("root source"));
        }
        finally {
            target.Release();
        }
    }

    [Test]
    public async Task Update_CopiesFromANestedArchiveIntoItsOuterArchive() {
        using var sandbox = new ZipTestSandbox();
        var archiveFileName = sandbox.PathFor("nested-to-root.zip");
        WriteArchive(
            archiveFileName,
            ("inner.zip", CreateArchiveBytes(("payload.txt", Text("nested source")))));
        using var source = await OpenSource(
            ZipTestSandbox.ProviderID(archiveFileName, nestedMembers: new[] { "inner.zip" }),
            "payload.txt");
        var target = ZipArchivePath.Get(new FileInfo(archiveFileName), new ZipArchiveNest(Array.Empty<string>()));
        try {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await Task.Run(() => target.Update(
                progress: null,
                token: timeout.Token,
                info: new ZipArchiveUpdate {
                    CopyStreamSets = new List<IEntryStreamSet> {
                        new ZipStreamSet(new IEntryStreamSource[] { source.Entry.Stream.Source })
                    }
                })).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(ReadEntry(archiveFileName, "payload.txt"), Is.EqualTo("nested source"));
        }
        finally {
            target.Release();
        }
    }

    [Test]
    public async Task Update_OpposingArchiveCopiesCompleteAfterBothSourcesAreReady() {
        using var sandbox = new ZipTestSandbox();
        var firstArchive = sandbox.PathFor("first.zip");
        var secondArchive = sandbox.PathFor("second.zip");
        WriteArchive(firstArchive, ("first.txt", Text("from first")));
        WriteArchive(secondArchive, ("second.txt", Text("from second")));
        using var firstSource = await OpenSource(firstArchive, "first.txt");
        using var secondSource = await OpenSource(secondArchive, "second.txt");
        var firstTarget = ZipArchivePath.Get(new FileInfo(firstArchive), new ZipArchiveNest(Array.Empty<string>()));
        var secondTarget = ZipArchivePath.Get(new FileInfo(secondArchive), new ZipArchiveNest(Array.Empty<string>()));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try {
            var barrier = new ReadBarrier();
            var firstUpdate = Task.Run(() => firstTarget.Update(
                progress: null,
                token: timeout.Token,
                info: new ZipArchiveUpdate {
                    CopyStreamSets = new List<IEntryStreamSet> {
                        new ZipStreamSet(new IEntryStreamSource[] {
                            new GatedSource(new ZipStreamSource(secondSource.Entry), barrier)
                        })
                    }
                }));
            var secondUpdate = Task.Run(() => secondTarget.Update(
                progress: null,
                token: timeout.Token,
                info: new ZipArchiveUpdate {
                    CopyStreamSets = new List<IEntryStreamSet> {
                        new ZipStreamSet(new IEntryStreamSource[] {
                            new GatedSource(new ZipStreamSource(firstSource.Entry), barrier)
                        })
                    }
                }));

            await Task.WhenAll(firstUpdate, secondUpdate).WaitAsync(TimeSpan.FromSeconds(15));
            using (Assert.EnterMultipleScope()) {
                Assert.That(ReadEntry(firstArchive, "second.txt"), Is.EqualTo("from second"));
                Assert.That(ReadEntry(secondArchive, "first.txt"), Is.EqualTo("from first"));
            }
        }
        finally {
            firstTarget.Release();
            secondTarget.Release();
        }
    }

    [Test]
    public async Task ZipSource_RetainsAndReleasesItsArchivePathAcrossProviderEndAndReuse() {
        using var sandbox = new ZipTestSandbox();
        var archiveFileName = sandbox.PathFor("queued-source.zip");
        WriteArchive(archiveFileName, ("payload.txt", Text("queued source")));
        using var source = await OpenSource(archiveFileName, "payload.txt");
        var originalPath = source.ArchivePath;
        var reusableSource = new ZipStreamSource(source.Entry);
        await ReadSource(reusableSource, CancellationToken.None);
        await ReadSource(reusableSource, CancellationToken.None);
        var queuedSource = new ZipStreamSource(source.Entry);
        var unvisitedSource = new ZipStreamSource(source.Entry);
        var queuedSet = new ZipStreamSet(new IEntryStreamSource[] { queuedSource });
        var unvisitedSet = new ZipStreamSet(new IEntryStreamSource[] { unvisitedSource });
        source.End();

        var reopenedID = await ZipID.Parse(archiveFileName, new ZipProviderFactory(), CancellationToken.None);
        Assert.That(reopenedID, Is.Not.Null);
        try {
            Assert.That(reopenedID.ArchivePath, Is.SameAs(originalPath));
        }
        finally {
            reopenedID.Release();
        }

        var targetFileName = sandbox.PathFor("queued-target.zip");
        WriteArchive(targetFileName, ("keep.txt", Text("keep")));
        var target = ZipArchivePath.Get(
            new FileInfo(targetFileName),
            new ZipArchiveNest(Array.Empty<string>()));
        try {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await target.Update(
                progress: null,
                token: timeout.Token,
                info: new ZipArchiveUpdate {
                    CopyStreamSets = new List<IEntryStreamSet> { queuedSet }
                });
            Assert.That(ReadEntry(targetFileName, "payload.txt"), Is.EqualTo("queued source"));
        }
        finally {
            target.Release();
        }

        await ReadSource(queuedSource, CancellationToken.None);
        var canceledTargetFileName = sandbox.PathFor("canceled-target.zip");
        WriteArchive(canceledTargetFileName, ("keep.txt", Text("keep")));
        var canceledTarget = ZipArchivePath.Get(
            new FileInfo(canceledTargetFileName),
            new ZipArchiveNest(Array.Empty<string>()));
        using (var canceled = new CancellationTokenSource()) {
            canceled.Cancel();
            try {
                await Assert.ThrowsAsync<OperationCanceledException>(() => canceledTarget.Update(
                    progress: null,
                    token: canceled.Token,
                    info: new ZipArchiveUpdate {
                        CopyStreamSets = new List<IEntryStreamSet> { unvisitedSet }
                    }));
            }
            finally {
                canceledTarget.Release();
            }
        }
        var releasedQueuedPath = ZipArchivePath.Get(
            new FileInfo(archiveFileName),
            new ZipArchiveNest(Array.Empty<string>()));
        try {
            Assert.That(releasedQueuedPath, Is.Not.SameAs(originalPath));
        }
        finally {
            releasedQueuedPath.Release();
        }

        await ReadSource(queuedSource, CancellationToken.None);

        var replacementPath = ZipArchivePath.Get(
            new FileInfo(archiveFileName),
            new ZipArchiveNest(Array.Empty<string>()));
        try {
            Assert.That(replacementPath, Is.Not.SameAs(originalPath));
        }
        finally {
            replacementPath.Release();
        }
    }


}
