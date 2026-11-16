using Brows.Zip;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Brows;

internal static class ZipEntryExtraction {
    internal static async Task ExtractAsync(
        ZipArchiveEntry entry,
        string destinationFileName,
        bool overwrite,
        IOperationProgress progress,
        CancellationToken token) {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFileName);

        var destination = Path.GetFullPath(destinationFileName);
        var directory = Path.GetDirectoryName(destination);
        var name = Path.GetFileName(destination);
        var destinationIsInvalid = string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(name);
        if (destinationIsInvalid) {
            throw new ArgumentException("The destination must name a file.", nameof(destinationFileName));
        }

        var staging = Path.Combine(directory, $".brows-{Guid.NewGuid():N}.tmp");
        var stagingOwned = false;
        try {
            await using (var output = new FileStream(
                staging,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan)) {
                stagingOwned = true;
                await using var input = new ZipEntryValidationStream(
                    await entry.OpenAsync(token),
                    entry.Length,
                    unchecked((uint)entry.Crc32));
                if (progress is not null) {
                    progress.Change(
                        addTarget: entry.Length,
                        data: entry.FullName,
                        kind: OperationProgressKind.FileSize);
                }
                await ZipArchiveUpdateRewriter.CopyBytesAsync(input, output, progress, false, token);
                await output.FlushAsync(token);
            }

            token.ThrowIfCancellationRequested();
            File.SetLastWriteTime(staging, entry.LastWriteTime.LocalDateTime);
            token.ThrowIfCancellationRequested();
            File.Move(staging, destination, overwrite);
            stagingOwned = false;
        }
        catch (Exception error) {
            if (stagingOwned) {
                try {
                    File.Delete(staging);
                }
                catch (Exception cleanupError) {
                    throw new AggregateException(error, cleanupError);
                }
            }
            throw;
        }
    }
}
