using Domore.IO;
using Domore.Logs;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class Zip : FileSystemCommand<ZipParameter> {
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
            output = output.Trim().Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            output = Path.IsPathFullyQualified(output)
                ? output
                : Path.Combine(directory.FullName, output);
            var outputExtEmpty = string.IsNullOrWhiteSpace(Path.GetExtension(output));
            if (outputExtEmpty) {
                output = Path.ChangeExtension(output, ext);
            }
            return context.Operate(async (progress, token) => {
                if (outputImplied) {
                    output = await FileSystemTask.Nonexistent(output, null, token);
                }
                return await Task.Run(cancellationToken: token, function: () => {
                    var entries = input.Entries(progress, token).ToList();
                    if (progress != null) {
                        progress.Target.Add(entries.Count);
                    }
                    using (var zip = ZipFile.Open(output, ZipArchiveMode.Create)) {
                        foreach (var entry in entries) {
                            if (Log.Info()) {
                                Log.Info(nameof(ZipFileExtensions.CreateEntryFromFile) + " > " + entry.EntryName);
                            }
                            if (progress != null) {
                                progress.Info.Data(entry.EntryName);
                            }
                            if (level.HasValue) {
                                zip.CreateEntryFromFile(entry.FilePath, entry.EntryName, level.Value);
                            }
                            else {
                                zip.CreateEntryFromFile(entry.FilePath, entry.EntryName);
                            }
                            if (progress != null) {
                                progress.Add(1);
                            }
                        }
                    }
                    return true;
                });
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
                    return await Task.Run(cancellationToken: token, function: () => {
                        Directory.CreateDirectory(target);
                        ZipFile.ExtractToDirectory(
                            sourceArchiveFileName: file.FullName,
                            destinationDirectoryName: target,
                            overwriteFiles: parameter.Overwrite == true);
                        return true;
                    });
                });
            }
            return worked;
        }

        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) return false;
            if (context.GetParameter(out var parameter) == false) return false;
            if (active.HasFileSystemSelection(out var selection) == false) {
                return false;
            }
            var action = parameter.Direction;
            if (action == null) {
                var allZip = selection.All(item => item is FileInfo file && file.Extension?.Equals(".zip", StringComparison.OrdinalIgnoreCase) == true);
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
    }
}
