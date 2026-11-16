using Brows.Operations;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Commands;

[TestFixture]
internal sealed class ZipTest {
    private sealed class CancelAfterCopyProgress : IOperationProgress {
        private readonly CancellationTokenSource Cancellation;
        private bool Cancelled;

        public CancelAfterCopyProgress(CancellationTokenSource cancellation) {
            Cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
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
            var receivedBytes = addProgress.GetValueOrDefault() > 0;
            if (receivedBytes && Cancelled is false) {
                Cancelled = true;
                Cancellation.Cancel();
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

    private ZipCommandTestSandbox Sandbox;

    [SetUp]
    public void SetUp() {
        Sandbox = new();
    }

    [TearDown]
    public void TearDown() {
        Sandbox.Dispose();
    }

    [TestCase(@"\\server\share\out.zip", @"D:\panel\current", @"\\server\share\out.zip")]
    [TestCase(@"\\server\share\out", @"D:\panel\current", @"\\server\share\out.zip")]
    [TestCase(@"\\?\UNC\server\share\out.zip", @"D:\panel", @"\\?\UNC\server\share\out.zip")]
    [TestCase(@"\\?\C:\exports\out.zip", @"D:\panel\current", @"\\?\C:\exports\out.zip")]
    [TestCase(@"C:\exports\out.zip", @"D:\panel\current", @"C:\exports\out.zip")]
    [TestCase(@"out.zip", @"D:\panel\current", @"D:\panel\current\out.zip")]
    [TestCase(@"out", @"D:\panel\current", @"D:\panel\current\out.zip")]
    [TestCase(@"child\..\out.zip", @"D:\panel\current", @"D:\panel\current\out.zip")]
    [TestCase(@"\exports\out.zip", @"D:\panel\current", @"D:\exports\out.zip")]
    [TestCase(@"/exports/out.zip", @"D:\panel\current", @"D:\exports\out.zip")]
    [TestCase(@"\exports\out.zip", @"\\server\share\panel", @"\\server\share\exports\out.zip")]
    [TestCase(@"D:out.zip", @"D:\panel\current", @"D:\panel\current\out.zip")]
    [TestCase(@"E:out.zip", @"D:\panel\current", @"E:\out.zip")]
    public void ResolveCompressionOutputPath_ResolvesAgainstTheActiveDirectory(
        string output,
        string directory,
        string expected) {
        Assert.That(Zip.ResolveCompressionOutputPath(output, directory), Is.EqualTo(expected));
    }

    [Test]
    public async Task CompressEntries_PreservesSelectedTreeWhenExtracted() {
        var directory = Sandbox.CreateDirectory("selected");
        Sandbox.WriteFile("selected/child/deeper/file.txt", "nested contents");
        Sandbox.CreateDirectory("selected/child/deeper/empty");
        var input = new ZipInput(true, new FileSystemInfo[] { new DirectoryInfo(directory) });
        var output = Sandbox.PathFor("result.zip");

        await Zip.CompressEntries(input.Entries(null, CancellationToken.None).ToList(),
            output, null, null, CancellationToken.None);

        using (var archive = ZipFile.OpenRead(output)) {
            Assert.That(archive.Entries.Select(entry => entry.FullName), Is.EquivalentTo(new[] {
                "child/deeper/file.txt",
                "child/deeper/empty/"
            }));
        }
        var extracted = Sandbox.PathFor("extracted");
        await ZipExtractor.ExtractAsync(output, extracted, false, null, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(Path.Combine(extracted, "child", "deeper", "file.txt")),
                Is.EqualTo("nested contents"));
            Assert.That(Directory.Exists(Path.Combine(extracted, "child", "deeper", "empty")), Is.True);
        }
    }

    [Test]
    public async Task CompressEntries_CreatesAnExplicitEmptyDirectory() {
        var directory = Sandbox.CreateDirectory("empty");
        var timestamp = new DateTime(2020, 1, 2, 3, 4, 6, DateTimeKind.Local);
        Directory.SetLastWriteTime(directory, timestamp);
        var input = new ZipInput(true, new FileSystemInfo[] {
            new DirectoryInfo(directory)
        });
        var output = Sandbox.PathFor("result.zip");

        await Zip.CompressEntries(
            input.Entries(null, CancellationToken.None).ToList(),
            output,
            null,
            null,
            CancellationToken.None);

        using var archive = ZipFile.OpenRead(output);
        var entry = archive.Entries.Single();
        using (Assert.EnterMultipleScope()) {
            Assert.That(entry.FullName, Is.EqualTo("empty/"));
            Assert.That(entry.Length, Is.EqualTo(0));
            Assert.That(entry.LastWriteTime.DateTime, Is.EqualTo(timestamp));
            Assert.That(entry.ExternalAttributes & (int)FileAttributes.Directory, Is.Not.Zero);
        }
    }

    [Test]
    public async Task CompressEntries_RefusesToReplaceExistingOutput() {
        var source = Sandbox.WriteFile("source.txt", "new contents");
        var output = Sandbox.WriteFile("result.zip", "existing output");
        var original = File.ReadAllBytes(output);
        var entries = new[] {
            new ZipEntry {
                EntryName = "source.txt",
                FilePath = source
            }
        };

        await Assert.ThrowsAsync<IOException>(
            () => Zip.CompressEntries(entries, output, null, null, CancellationToken.None));

        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllBytes(output), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(Sandbox.Root, ".brows-*.tmp").Length, Is.Zero);
        }
    }

    [Test]
    public async Task CompressEntries_PreservesSourceFileMetadata() {
        var source = Sandbox.WriteFile("source.txt", "contents");
        var timestamp = new DateTime(2020, 1, 2, 3, 4, 6, DateTimeKind.Local);
        File.SetLastWriteTime(source, timestamp);
        var output = Sandbox.PathFor("result.zip");
        var entries = new[] {
            new ZipEntry {
                EntryName = "source.txt",
                FilePath = source
            }
        };

        await Zip.CompressEntries(entries, output, null, null, CancellationToken.None);

        using var archive = ZipFile.OpenRead(output);
        var entry = archive.Entries.Single();
        using (Assert.EnterMultipleScope()) {
            Assert.That(entry.LastWriteTime.DateTime, Is.EqualTo(timestamp));
            Assert.That(entry.ExternalAttributes, Is.EqualTo((int)File.GetAttributes(source)));
        }
    }

    [Test]
    public async Task CompressEntries_CancellationPreservesExistingOutputAndRemovesStagingFile() {
        var source = Sandbox.WriteFile("source.bin", new string('x', 1024 * 1024));
        var output = Sandbox.WriteFile("result.zip", "existing output");
        var original = File.ReadAllBytes(output);
        var entries = new[] {
            new ZipEntry {
                EntryName = "source.bin",
                FilePath = source
            }
        };
        using var cancellation = new CancellationTokenSource();
        var progress = new CancelAfterCopyProgress(cancellation);

        await Assert.CatchAsync<OperationCanceledException>(async () => {
            await Zip.CompressEntries(entries, output, null, progress, cancellation.Token);
        });

        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllBytes(output), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(Sandbox.Root, ".brows-*.tmp").Length, Is.Zero);
        }
    }
}
