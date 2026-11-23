using Brows.Zip;
using Domore.Conf;
using Domore.Conf.Cli;
using Domore.Logs;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Commands;

internal sealed class Zip : FileSystemCommand<Zip.Parameter> {
    private static readonly ILog Log = Logging.For(typeof(Zip));

    private bool Compress(Context context) {
        if (context == null) return false;
        if (context.HasPanel(out var active) == false) return false;
        if (context.GetParameter(out var parameter) == false) return false;
        if (active.HasFileSystemDirectory(out var directory) == false) return false;
        if (active.HasFileSystemSelection(out var selection) == false) return false;
        if (active.HasFileSystemCaseSensitivity(out var caseSensitive) == false) return false;
        var ext = ".zip";
        var input = new ZipInput(caseSensitive.Value, selection);
        var level = parameter.CompressionLevel;
        var outputImplied = false;
        var output = parameter.Output?.Trim() ?? "";
        if (output == "") {
            output = Path.ChangeExtension(selection[0].Name, ext);
            outputImplied = true;
        }
        output = ResolveCompressionOutputPath(output, directory.FullName);
        return context.Operate(async (progress, token) => {
            progress?.Change(kind: OperationProgressKind.FileSize);
            if (outputImplied) {
                output = await FileSystemTask.Nonexistent(output, null, token);
            }
            var entries = await Task.Run(
                () => input.Entries(progress, token).ToList(),
                token);
            await CompressEntries(entries, output, level, progress, token);
            return true;
        });
    }

    private bool Extract(Context context) {
        if (context == null) return false;
        if (context.HasPanel(out var active) == false) return false;
        if (context.GetParameter(out var parameter) == false) return false;
        if (active.HasFileSystemDirectory(out var directory) == false) return false;
        if (active.HasFileSystemSelection(out var selection) == false) return false;
        var files = selection.OfType<FileInfo>().ToList();
        if (files.Count == 0) {
            return false;
        }
        var worked = false;
        foreach (var file in files) {
            worked = worked | context.Operate(async (progress, token) => {
                progress?.Change(kind: OperationProgressKind.FileSize);
                var target = parameter.Output?.Trim() ?? "";
                var targetImplied = false;
                if (target == "") {
                    targetImplied = true;
                    target = Path.GetFileNameWithoutExtension(file.FullName);
                }
                var targetFullyQualified = Path.IsPathFullyQualified(target);
                if (targetFullyQualified == false) {
                    target = Path.Combine(directory.FullName, target);
                }
                if (targetImplied) {
                    target = await FileSystemTask.Nonexistent(target, null, token);
                }
                await Task.Run(async () => {
                    await ZipExtractor.ExtractAsync(
                        file.FullName,
                        target,
                        parameter.Overwrite == true,
                        progress,
                        token);
                }, token);
                return true;
            });
        }
        return worked;
    }

    internal static string ResolveCompressionOutputPath(string output, string directory) {
        output = Path.GetFullPath(output.Trim(), directory);
        var outputExtEmpty = string.IsNullOrWhiteSpace(Path.GetExtension(output));
        if (outputExtEmpty) {
            output = Path.ChangeExtension(output, ".zip");
        }
        return output;
    }

    internal static async Task CompressEntries(
        IReadOnlyList<ZipEntry> entries,
        string output,
        CompressionLevel? level,
        IOperationProgress progress,
        CancellationToken token) {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(output);
        var outputPath = Path.GetFullPath(output);
        var outputDirectory = Path.GetDirectoryName(outputPath);
        var stagingPath = Path.Combine(outputDirectory, $".brows-{Guid.NewGuid():N}.tmp");
        var stagingOwned = false;
        try {
            token.ThrowIfCancellationRequested();
            await using (var outputStream = new FileStream(
                stagingPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan)) {
                stagingOwned = true;
                using (var zip = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true)) {
                    foreach (var entry in entries) {
                        token.ThrowIfCancellationRequested();
                        if (Log.Info()) {
                            Log.Info(nameof(ZipFileExtensions.CreateEntryFromFile) + " > " + entry.EntryName);
                        }
                        if (progress is not null) {
                            progress.Change(data: entry.EntryName);
                        }
                        var entryName = entry.IsDirectory && entry.EntryName.EndsWith('/') == false
                            ? entry.EntryName + "/"
                            : entry.EntryName;
                        var compressionLevel = level ?? CompressionLevel.Optimal;
                        var zipEntry = zip.CreateEntry(entryName, compressionLevel);
                        var sourceInfo = entry.IsDirectory
                            ? (FileSystemInfo)new DirectoryInfo(entry.FilePath)
                            : new FileInfo(entry.FilePath);
                        sourceInfo.Refresh();
                        if (sourceInfo.Exists is false) {
                            throw new FileNotFoundException(
                                "The source for a ZIP entry no longer exists.",
                                sourceInfo.FullName);
                        }
                        zipEntry.LastWriteTime = sourceInfo.LastWriteTime;
                        zipEntry.ExternalAttributes = (int)sourceInfo.Attributes;
                        if (entry.IsDirectory == false) {
                            await using var sourceStream = new FileStream(
                                sourceInfo.FullName,
                                FileMode.Open,
                                FileAccess.Read,
                                FileShare.Read,
                                bufferSize: 81920,
                                FileOptions.Asynchronous | FileOptions.SequentialScan);
                            await using var destinationStream = zipEntry.Open();
                            if (progress is not null) {
                                progress.Change(addTarget: sourceStream.Length);
                            }
                            await ZipArchiveUpdateRewriter.CopyBytesAsync(
                                sourceStream,
                                destinationStream,
                                progress,
                                addTargetDynamically: false,
                                token);
                        }
                        token.ThrowIfCancellationRequested();
                    }
                }
                await outputStream.FlushAsync(token);
            }
            token.ThrowIfCancellationRequested();
            File.Move(stagingPath, outputPath);
        }
        finally {
            var ownedStagingFileExists = stagingOwned && File.Exists(stagingPath);
            if (ownedStagingFileExists) {
                File.Delete(stagingPath);
            }
        }
    }

    protected sealed override IEnumerable<Type> Source { get; } = new[] {
        typeof(IEntry)
    };

    protected sealed override bool Work(Context context) {
        if (context == null) return false;
        if (context.HasPanel(out var active) == false) return false;
        if (context.GetParameter(out var parameter) == false) return false;
        if (active.HasFileSystemSelection(out var selection) == false) {
            return false;
        }
        var action = parameter.Direction;
        if (action == null) {
            var allZip = selection.All(item =>
                item is FileInfo file &&
                file.Extension?.Equals(".zip", StringComparison.OrdinalIgnoreCase) == true);
            if (allZip) {
                action = ZipDirection.Extract;
            }
            else {
                action = ZipDirection.Compress;
            }
        }
        switch (action) {
            case ZipDirection.Compress:
                return Compress(context);
            case ZipDirection.Extract:
                return Extract(context);
            default:
                return false;
        }
    }

    public sealed class Parameter {
        [CliArgument]
        public string Output { get; set; }

        [Conf("force")]
        public ZipDirection? Direction { get; set; }

        [Conf("compression")]
        public CompressionLevel? CompressionLevel { get; set; }

        public bool? Overwrite { get; set; }
    }
}
