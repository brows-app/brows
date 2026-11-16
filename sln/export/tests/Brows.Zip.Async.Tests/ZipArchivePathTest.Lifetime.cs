using Brows.Entries;
using Brows.Zip;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed partial class ZipArchivePathTest {
    private static bool ArchiveLockerHasWaiters(ArchiveLocker archiveLocker) {
        /*
         * Observe the queued waiter itself; scheduling StreamReady does not prove its worker reached the lock.
         */
        var lockerField = typeof(ArchiveLocker).GetField("Locker", BindingFlags.Instance | BindingFlags.NonPublic);
        if (lockerField is null) {
            throw new MissingFieldException(typeof(ArchiveLocker).FullName, "Locker");
        }
        var locker = (SemaphoreSlim)lockerField.GetValue(archiveLocker);
        var semaphoreFields = typeof(SemaphoreSlim).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
        var waitCountField = semaphoreFields.SingleOrDefault(field =>
            field.Name.EndsWith("waitCount", StringComparison.OrdinalIgnoreCase));
        var asyncHeadField = semaphoreFields.SingleOrDefault(field =>
            field.Name.EndsWith("asyncHead", StringComparison.OrdinalIgnoreCase));
        var synchronousWaiterCount = (int?)waitCountField?.GetValue(locker) ?? 0;
        var hasAsyncWaiter = asyncHeadField?.GetValue(locker) is not null;
        var hasWaiter = synchronousWaiterCount > 0 || hasAsyncWaiter;
        return hasWaiter;
    }

    [Test]
    public async Task StreamSource_CancellationWhileWaitingForArchiveLockReleasesTheSourceForRetry() {
        using var sandbox = new UpdateTestSandbox();
        var archiveFileName = sandbox.PathFor("cancel-wait.zip");
        File.WriteAllBytes(
            archiveFileName,
            CreateArchive("payload.bin", Encoding.UTF8.GetBytes("retry payload"), CompressionLevel.NoCompression));
        var path = Open(archiveFileName);
        ArchiveLocker archiveLocker = null;
        ArchiveLocked held = null;
        Task<IEntryStreamReady> pending = null;
        try {
            var info = (await path.EntryInfo(CancellationToken.None))
                .Items.Single(item => item.Kind == ZipEntryKind.File);
            var source = new TestArchiveStreamSource(info);
            archiveLocker = ArchiveLocker.Get(new FileInfo(archiveFileName));
            held = archiveLocker.Lock();
            using var cancellation = new CancellationTokenSource();
            var waiting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending = Task.Run(async () => {
                var streamReady = ((IEntryStreamSource)source).StreamReady(cancellation.Token);
                waiting.TrySetResult(true);
                return await streamReady;
            });
            await waiting.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var waitStarted = await Task.Run(() => SpinWait.SpinUntil(
                () => ArchiveLockerHasWaiters(archiveLocker),
                TimeSpan.FromSeconds(5)));
            Assert.That(waitStarted, Is.True, "The stream source did not reach the held archive lock.");
            cancellation.Cancel();
            await Assert.CatchAsync<OperationCanceledException>(async () => {
                await pending.WaitAsync(TimeSpan.FromSeconds(5));
            });

            held.Dispose();
            held = null;
            archiveLocker.Release();
            archiveLocker = null;

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var ready = await ((IEntryStreamSource)source).StreamReady(timeout.Token);
            await using var stream = ((IEntryStreamSource)source).Stream;
            await stream.CopyToAsync(Stream.Null, timeout.Token);
        }
        finally {
            held?.Dispose();
            if (pending is not null && pending.IsCompleted == false) {
                try {
                    (await pending.WaitAsync(TimeSpan.FromSeconds(5))).Dispose();
                }
                catch {
                }
            }
            archiveLocker?.Release();
            path.Release();
        }
    }

    [Test]
    public async Task StreamSource_MissingEntryReleasesTheArchiveLock() {
        using var sandbox = new UpdateTestSandbox();
        var archiveFileName = sandbox.PathFor("missing-entry.zip");
        File.WriteAllBytes(
            archiveFileName,
            CreateArchive("present.txt", Encoding.UTF8.GetBytes("present"), CompressionLevel.NoCompression));
        var path = Open(archiveFileName);
        try {
            var foreignArchiveBytes = CreateArchive(
                "missing.txt",
                Encoding.UTF8.GetBytes("stale metadata"),
                CompressionLevel.NoCompression);
            using var foreignStream = new MemoryStream(foreignArchiveBytes, writable: false);
            using var foreignArchive = new ZipArchive(foreignStream, ZipArchiveMode.Read);
            var info = ZipEntryInfo.File(
                path,
                new ZipEntryName("missing.txt"),
                foreignArchive.Entries.Single());
            var source = new TestArchiveStreamSource(info);
            await Assert.CatchAsync<Exception>(async () => {
                using var ready = await ((IEntryStreamSource)source).StreamReady(CancellationToken.None);
            });

            var destination = sandbox.PathFor("output/present.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await path.Read(
                new ZipArchiveRead {
                    ExtractEntriesToFiles = new() {
                        ["present.txt"] = destination
                    }
                },
                progress: null,
                token: timeout.Token);
            Assert.That(File.ReadAllText(destination), Is.EqualTo("present"));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task StreamSource_InvalidArchiveDuringPreparationReleasesTheArchiveLock() {
        using var sandbox = new UpdateTestSandbox();
        var archiveFileName = sandbox.PathFor("invalid-preparation.zip");
        var original = CreateArchive(
            "payload.txt",
            Encoding.UTF8.GetBytes("valid after retry"),
            CompressionLevel.NoCompression);
        File.WriteAllBytes(archiveFileName, original);
        var path = Open(archiveFileName);
        try {
            var info = (await path.EntryInfo(CancellationToken.None))
                .Items.Single(item => item.Kind == ZipEntryKind.File);
            File.WriteAllBytes(archiveFileName, new byte[] { 1, 2, 3, 4 });
            var source = new TestArchiveStreamSource(info);
            await Assert.CatchAsync<Exception>(async () => {
                using var ready = await ((IEntryStreamSource)source).StreamReady(CancellationToken.None);
            });

            File.WriteAllBytes(archiveFileName, original);
            var destination = sandbox.PathFor("output/payload.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await path.Read(
                new ZipArchiveRead {
                    ExtractEntriesToFiles = new() {
                        ["payload.txt"] = destination
                    }
                },
                progress: null,
                token: timeout.Token);
            Assert.That(File.ReadAllText(destination), Is.EqualTo("valid after retry"));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task StreamSource_EntryOpenFailureReleasesTheArchiveLock() {
        using var sandbox = new UpdateTestSandbox();
        var archiveFileName = sandbox.PathFor("entry-open.zip");
        var original = CreateArchive(
            "payload.txt",
            Encoding.UTF8.GetBytes("valid after retry"),
            CompressionLevel.NoCompression);
        File.WriteAllBytes(archiveFileName, original);
        var path = Open(archiveFileName);
        try {
            var info = (await path.EntryInfo(CancellationToken.None))
                .Items.Single(item => item.Kind == ZipEntryKind.File);
            var invalid = (byte[])original.Clone();
            invalid[0] ^= 0xFF;
            File.WriteAllBytes(archiveFileName, invalid);
            var source = new TestArchiveStreamSource(info);
            await Assert.CatchAsync<Exception>(async () => {
                using var ready = await ((IEntryStreamSource)source).StreamReady(CancellationToken.None);
            });

            File.WriteAllBytes(archiveFileName, original);
            var destination = sandbox.PathFor("output/payload.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await path.Read(
                new ZipArchiveRead {
                    ExtractEntriesToFiles = new() {
                        ["payload.txt"] = destination
                    }
                },
                progress: null,
                token: timeout.Token);
            Assert.That(File.ReadAllText(destination), Is.EqualTo("valid after retry"));
        }
        finally {
            path.Release();
        }
    }
}
