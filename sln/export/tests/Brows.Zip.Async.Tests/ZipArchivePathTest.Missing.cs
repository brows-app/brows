using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed partial class ZipArchivePathTest {
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task Read_MissingEntryFailsWithoutChangingTheDestination(bool destinationExists, bool overwrite) {
        var archive = CreateArchive("missing-read.zip", ("keep.txt", "archive contents"));
        var destination = UpdateSandbox.PathFor("output/absent.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (destinationExists) {
            File.WriteAllText(destination, "existing destination");
        }
        var path = OpenUpdateArchive(archive);
        var progress = new ByteProgress();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try {
            var error = await Assert.ThrowsAsync<FileNotFoundException>(() => path.Read(new ZipArchiveRead {
                ExtractOverwrites = overwrite,
                ExtractEntriesToFiles = new() { ["missing/absent.txt"] = destination }
            }, progress, timeout.Token));

            using (Assert.EnterMultipleScope()) {
                Assert.That(error!.FileName, Is.EqualTo("missing/absent.txt"));
                Assert.That(error.Message, Does.Contain("missing/absent.txt"));
                Assert.That(error.Message, Does.Contain(path.FullName));
                Assert.That(progress.BytesCopied, Is.Zero);
                Assert.That(progress.BytesTarget, Is.Zero);
                Assert.That(File.Exists(destination), Is.EqualTo(destinationExists));
                if (destinationExists) {
                    Assert.That(File.ReadAllText(destination), Is.EqualTo("existing destination"));
                }
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(destination)!, ".brows-*.tmp"), Is.Empty);
            }
        }
        finally {
            path.Release();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Read_MissingLaterEntryFailsBeforePublishingAnyOutput(bool nested) {
        var archive = nested
            ? CreateNestedArchive("missing-batch.zip", "inside.zip", ("keep.txt", "archive contents"))
            : CreateArchive("missing-batch.zip", ("keep.txt", "archive contents"));
        var destination = UpdateSandbox.WriteFile("output/keep.txt", "existing destination");
        var missingDestination = UpdateSandbox.PathFor("output/absent.txt");
        var path = OpenUpdateArchive(archive, nested ? new[] { "inside.zip" } : Array.Empty<string>());
        var progress = new ByteProgress();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try {
            var error = await Assert.ThrowsAsync<FileNotFoundException>(() => path.Read(new ZipArchiveRead {
                ExtractOverwrites = true,
                ExtractEntriesToFiles = new() {
                    ["keep.txt"] = destination,
                    ["absent.txt"] = missingDestination
                }
            }, progress, timeout.Token));

            using (Assert.EnterMultipleScope()) {
                Assert.That(error!.FileName, Is.EqualTo("absent.txt"));
                Assert.That(File.ReadAllText(destination), Is.EqualTo("existing destination"));
                Assert.That(File.Exists(missingDestination), Is.False);
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(destination)!, ".brows-*.tmp"), Is.Empty);
            }
        }
        finally {
            path.Release();
        }
    }
}
