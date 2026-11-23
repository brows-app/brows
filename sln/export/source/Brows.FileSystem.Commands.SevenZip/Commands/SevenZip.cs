using Domore.Conf;
using Domore.Conf.Cli;
using Domore.Logs;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Commands;

internal sealed class SevenZip : FileSystemCommand<SevenZip.Parameter> {
    private static readonly ILog Log = Logging.For(typeof(SevenZip));

    private bool Compress(Context context) {
        if (context is null) {
            return false;
        }
        if (context.HasPanel(out var active) == false) {
            return false;
        }
        if (context.GetParameter(out var parameter) == false) {
            return false;
        }
        if (active.HasFileSystemDirectory(out var directory) == false) {
            return false;
        }
        if (active.HasFileSystemSelection(out var selection) == false) {
            return false;
        }
        if (active.HasFileSystemCaseSensitivity(out var caseSensitive) == false) {
            return false;
        }
        var ext = ".7z";
        var input = new SevenZipInput(caseSensitive.Value, selection);
        var level = parameter.CompressionLevel;
        var outputImplied = false;
        var output = parameter.Output?.Trim() ?? "";
        if (output == "") {
            output = Path.ChangeExtension(selection[0].Name, ext);
            outputImplied = true;
        }
        output = ResolveOutputPath(directory.FullName, output);
        return context.Operate(async (progress, token) => {
            if (outputImplied) {
                output = await FileSystemTask.Nonexistent(output, null, token);
            }
            var entries = input.Entries(progress, token).ToList();
            if (Log.Info()) {
                Log.Info(nameof(entries) + " > " + output);
            }
            var files = entries.ToDictionary(entry => entry.EntryName, entry => entry.FilePath);
            await SevenZipArchiveFile.Create(output, files, level, progress, token);
            return true;
        });
    }

    private bool Extract(Context context) {
        if (context is null) {
            return false;
        }
        if (context.HasPanel(out var active) == false) {
            return false;
        }
        if (context.GetParameter(out var parameter) == false) {
            return false;
        }
        if (active.HasFileSystemDirectory(out var directory) == false) {
            return false;
        }
        if (active.HasFileSystemSelection(out var selection) == false) {
            return false;
        }
        var files = selection.OfType<FileInfo>().ToList();
        if (files.Count == 0) {
            return false;
        }
        var worked = false;
        foreach (var file in files) {
            worked = worked | context.Operate(async (progress, token) => {
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
                if (Log.Info()) {
                    Log.Info(nameof(SevenZipArchiveFile.Extract) + " > " + file.FullName);
                }
                await SevenZipArchiveFile.Extract(
                    archivePath: file.FullName,
                    directory: target,
                    overwrite: parameter.Overwrite == true,
                    progress: progress,
                    token: token);
                return true;
            });
        }
        return worked;
    }

    private static bool IsSevenZipFile(FileSystemInfo item) {
        if (item is not FileInfo file) {
            return false;
        }
        return file.Extension?.Equals(".7z", StringComparison.OrdinalIgnoreCase) == true;
    }

    internal static string ResolveOutputPath(string directory, string output) {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(output);
        output = output.Trim();
        if (Path.IsPathFullyQualified(output) == false) {
            var pathRooted = Path.IsPathRooted(output);
            if (pathRooted) {
                var outputRoot = Path.GetPathRoot(output) ?? "";
                var rootedOutput = output.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var driveRelative = outputRoot.Length == 2 && outputRoot[1] == ':';
                if (driveRelative) {
                    var directoryRoot = Path.GetPathRoot(directory) ?? "";
                    var driveMatches = string.Equals(
                        outputRoot,
                        directoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase);
                    var baseDirectory = driveMatches
                        ? directory
                        : outputRoot + Path.DirectorySeparatorChar;
                    output = Path.Combine(baseDirectory, rootedOutput[outputRoot.Length..]);
                }
                else {
                    var directoryRoot = Path.GetPathRoot(directory) ?? "";
                    output = Path.Combine(directoryRoot, rootedOutput);
                }
            }
            else {
                output = Path.Combine(directory, output);
            }
        }
        var outputExtEmpty = string.IsNullOrWhiteSpace(Path.GetExtension(output));
        if (outputExtEmpty) {
            output = Path.ChangeExtension(output, ".7z");
        }
        return output;
    }

    protected sealed override IEnumerable<Type> Source { get; } = new[] {
        typeof(IEntry)
    };

    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (context.HasPanel(out var active) == false) {
            return false;
        }
        if (context.GetParameter(out var parameter) == false) {
            return false;
        }
        if (active.HasFileSystemSelection(out var selection) == false) {
            return false;
        }
        var action = parameter.Direction;
        if (action is null) {
            var allSevenZip = selection.All(IsSevenZipFile);
            if (allSevenZip) {
                action = SevenZipDirection.Extract;
            }
            else {
                action = SevenZipDirection.Compress;
            }
        }
        switch (action) {
            case SevenZipDirection.Compress:
                return Compress(context);
            case SevenZipDirection.Extract:
                return Extract(context);
            default:
                return false;
        }
    }

    public sealed record Parameter {
        [CliArgument]
        public string Output { get; init; }

        [Conf("force")]
        public SevenZipDirection? Direction { get; init; }

        [Conf("compression")]
        public int? CompressionLevel { get; init; }

        public bool? Overwrite { get; init; }
    }
}
