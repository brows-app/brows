using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Brows.Zip;

internal sealed class ZipArchiveUpdateRewriter {
    private const int CopyBufferSize = 81920;

    private readonly ArchiveUpdateStaging Staging;
    private readonly IOperationProgress Progress;

    private static void CopyMetadata(ZipArchiveEntry source, ZipArchiveEntry destination) {
        /*
         * ZipArchive exposes comments, attributes, and timestamps, but not the raw extra fields or the original
         * compression method. Recreate each entry while preserving the metadata available through its public API.
         */
        destination.Comment = source.Comment;
        destination.ExternalAttributes = source.ExternalAttributes;
        destination.LastWriteTime = source.LastWriteTime;
    }

    private async Task CopyEntryAsync(
        ZipArchiveEntry source,
        ZipArchive destinationArchive,
        string destinationName,
        string replacementArchiveFileName,
        CancellationToken token) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destinationArchive);
        token.ThrowIfCancellationRequested();
        if (Progress is not null) {
            var sourceLength = replacementArchiveFileName is null
                ? source.Length
                : new FileInfo(replacementArchiveFileName).Length;
            Progress.Change(
                addTarget: sourceLength,
                data: source.FullName,
                kind: OperationProgressKind.FileSize);
        }
        var destination = destinationArchive.CreateEntry(destinationName, CompressionLevel.Optimal);
        CopyMetadata(source, destination);
        await using var output = await destination.OpenAsync(token);
        if (replacementArchiveFileName is not null) {
            await using var input = new FileStream(
                replacementArchiveFileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await CopyBytesAsync(input, output, Progress, false, token);
            return;
        }

        await using var entry = new ZipEntryValidationStream(
            await source.OpenAsync(token),
            source.Length,
            unchecked((uint)source.Crc32));
        await CopyBytesAsync(entry, output, Progress, false, token);
    }

    private async Task RewriteTargetAsync(
        string sourceArchiveFileName,
        FileStream destinationFile,
        IReadOnlyDictionary<string, string> additions,
        ISet<string> removedNames,
        CancellationToken token) {
        await using var sourceFile = new FileStream(
            sourceArchiveFileName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sourceArchive = new ZipArchive(sourceFile, ZipArchiveMode.Read);
        using (var destinationArchive = new ZipArchive(destinationFile, ZipArchiveMode.Create, leaveOpen: true)) {
            destinationArchive.Comment = sourceArchive.Comment;
            foreach (var entry in sourceArchive.Entries) {
                token.ThrowIfCancellationRequested();
                var shouldRemove = removedNames.Contains(entry.FullName);
                if (shouldRemove) {
                    if (Progress is not null) {
                        Progress.Change(data: entry.FullName);
                    }
                    continue;
                }
                await CopyEntryAsync(entry, destinationArchive, entry.FullName, null, token);
            }

            foreach (var addition in additions) {
                token.ThrowIfCancellationRequested();
                var isDirectory = addition.Value is null;
                var needsDirectorySeparator = isDirectory && addition.Key.EndsWith('/') == false;
                var name = needsDirectorySeparator ? addition.Key + "/" : addition.Key;
                if (Progress is not null) {
                    Progress.Change(data: name);
                }
                var entry = destinationArchive.CreateEntry(name, CompressionLevel.Optimal);
                if (isDirectory) {
                    entry.ExternalAttributes = (int)FileAttributes.Directory;
                    continue;
                }

                var file = new FileInfo(addition.Value);
                file.Refresh();
                if (file.Exists == false) {
                    throw new FileNotFoundException("The source file for a ZIP entry no longer exists.", file.FullName);
                }
                entry.LastWriteTime = file.LastWriteTime;
                entry.ExternalAttributes = (int)file.Attributes;
                var length = file.Length;
                if (Progress is not null) {
                    Progress.Change(addTarget: length, data: name);
                }
                await using var input = new FileStream(
                    file.FullName,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    CopyBufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await using var output = await entry.OpenAsync(token);
                await CopyBytesAsync(input, output, Progress, false, token);
            }
        }
        token.ThrowIfCancellationRequested();
        await destinationFile.FlushAsync(token);
    }

    private async Task RewriteParentAsync(
        string sourceArchiveFileName,
        string nestedEntryName,
        string nestedArchiveFileName,
        FileStream destinationFile,
        CancellationToken token) {
        await using var sourceFile = new FileStream(
            sourceArchiveFileName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sourceArchive = new ZipArchive(sourceFile, ZipArchiveMode.Read);
        var nestedEntry = sourceArchive.GetEntry(nestedEntryName)
            ?? throw new InvalidDataException($"Nested archive entry '{nestedEntryName}' was not found.");
        using (var destinationArchive = new ZipArchive(destinationFile, ZipArchiveMode.Create, leaveOpen: true)) {
            destinationArchive.Comment = sourceArchive.Comment;
            foreach (var entry in sourceArchive.Entries) {
                token.ThrowIfCancellationRequested();
                var isSelectedNestedEntry = ReferenceEquals(entry, nestedEntry);
                if (isSelectedNestedEntry) {
                    await CopyEntryAsync(
                        entry,
                        destinationArchive,
                        entry.FullName,
                        nestedArchiveFileName,
                        token);
                    continue;
                }
                await CopyEntryAsync(entry, destinationArchive, entry.FullName, null, token);
            }
        }
        token.ThrowIfCancellationRequested();
        await destinationFile.FlushAsync(token);
    }

    private async Task<string> RewriteNestedAsync(
        string sourceArchiveFileName,
        ZipArchiveNest nest,
        IReadOnlyDictionary<string, string> additions,
        ISet<string> removedNames,
        CancellationToken token) {
        var nesting = new List<string>(nest);
        if (nesting.Count == 0) {
            var destination = Staging.CreateArchiveFile(new FileInfo(sourceArchiveFileName));
            await using (destination) {
                await RewriteTargetAsync(sourceArchiveFileName, destination, additions, removedNames, token);
            }
            return destination.Name;
        }

        var archiveAtLevel = new List<string> { sourceArchiveFileName };
        foreach (var nestedName in nesting) {
            token.ThrowIfCancellationRequested();
            var parentArchiveFileName = archiveAtLevel[^1];
            await using var parentFile = new FileStream(
                parentArchiveFileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var parentArchive = new ZipArchive(parentFile, ZipArchiveMode.Read);
            var nestedEntry = parentArchive.GetEntry(nestedName)
                ?? throw new InvalidDataException($"Nested archive entry '{nestedName}' was not found.");
            var nestedFile = Staging.CreateSourceFile();
            await using (nestedFile) {
                if (Progress is not null) {
                    Progress.Change(addTarget: nestedEntry.Length, data: nestedEntry.FullName);
                }
                await using var nestedContent = new ZipEntryValidationStream(
                    await nestedEntry.OpenAsync(token),
                    nestedEntry.Length,
                    unchecked((uint)nestedEntry.Crc32));
                await CopyBytesAsync(
                    nestedContent,
                    nestedFile,
                    Progress,
                    false,
                    token);
                await nestedFile.FlushAsync(token);
            }
            archiveAtLevel.Add(nestedFile.Name);
        }

        var innermostArchive = Staging.CreateSourceFile();
        await using (innermostArchive) {
            await RewriteTargetAsync(archiveAtLevel[^1], innermostArchive, additions, removedNames, token);
        }
        var rewrittenArchiveFileName = innermostArchive.Name;

        for (var index = nesting.Count - 1; index >= 0; index--) {
            token.ThrowIfCancellationRequested();
            var parentArchiveFileName = archiveAtLevel[index];
            var isOutermost = index == 0;
            var parentStage = isOutermost
                ? Staging.CreateArchiveFile(new FileInfo(sourceArchiveFileName))
                : Staging.CreateSourceFile();
            await using (parentStage) {
                await RewriteParentAsync(
                    parentArchiveFileName,
                    nesting[index],
                    rewrittenArchiveFileName,
                    parentStage,
                    token);
            }
            rewrittenArchiveFileName = parentStage.Name;
        }
        return rewrittenArchiveFileName;
    }

    internal ZipArchiveUpdateRewriter(ArchiveUpdateStaging staging, IOperationProgress progress) {
        Staging = staging ?? throw new ArgumentNullException(nameof(staging));
        Progress = progress;
    }

    internal static async Task CopyBytesAsync(
        Stream source,
        Stream destination,
        IOperationProgress progress,
        bool addTargetDynamically,
        CancellationToken token) {
        var buffer = new byte[CopyBufferSize];
        for (; ; ) {
            token.ThrowIfCancellationRequested();
            var read = await source.ReadAsync(buffer.AsMemory(), token);
            if (read == 0) {
                break;
            }
            token.ThrowIfCancellationRequested();
            await destination.WriteAsync(buffer.AsMemory(0, read), token);
            if (progress is not null) {
                if (addTargetDynamically) {
                    progress.Change(addTarget: read, addProgress: read);
                }
                else {
                    progress.Change(addProgress: read);
                }
            }
        }
    }

    internal Task<string> RewriteAsync(
        string sourceArchiveFileName,
        ZipArchiveNest nest,
        IReadOnlyDictionary<string, string> additions,
        ISet<string> removedNames,
        CancellationToken token) {
        ArgumentNullException.ThrowIfNull(sourceArchiveFileName);
        ArgumentNullException.ThrowIfNull(nest);
        ArgumentNullException.ThrowIfNull(additions);
        ArgumentNullException.ThrowIfNull(removedNames);
        return RewriteNestedAsync(sourceArchiveFileName, nest, additions, removedNames, token);
    }
}
