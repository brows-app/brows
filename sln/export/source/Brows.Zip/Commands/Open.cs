using Brows.Shared.Archives;
using Domore.Conf.Cli;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Commands;

internal sealed class Open : ZipCommand<Open.Parameter> {
    private static void EnsureContained(string root, string path) {
        var rootWithSeparator = Path.EndsInDirectorySeparator(root)
            ? root
            : root + Path.DirectorySeparatorChar;
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var isContained = path.StartsWith(rootWithSeparator, pathComparison);
        if (isContained is false) {
            throw new IOException("The temporary file is outside its owned directory.");
        }
    }

    internal static string SafeExtension(string name) {
        var extension = Path.GetExtension(name);
        var maximumExtensionLength = 255 - 32;
        var hasInvalidCharacter = extension.Any(character =>
            char.IsControl(character) ||
            character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*');
        var extensionIsSafe = extension.Length > 1 &&
                              extension.Length <= maximumExtensionLength &&
                              extension.EndsWith('.') is false &&
                              extension.EndsWith(' ') is false &&
                              hasInvalidCharacter is false;
        return extensionIsSafe ? extension : string.Empty;
    }

    internal static FileInfo CreateTemporaryFile(string temporaryDirectory,
                                                 string entryName,
                                                 CancellationToken token) {
        if (temporaryDirectory is null) {
            throw new ArgumentNullException(nameof(temporaryDirectory));
        }
        var fullTemporaryDirectory = Path.GetFullPath(temporaryDirectory);
        Directory.CreateDirectory(fullTemporaryDirectory);
        var temporaryDirectoryAttributes = File.GetAttributes(fullTemporaryDirectory);
        var temporaryDirectoryIsReparsePoint = temporaryDirectoryAttributes.HasFlag(FileAttributes.ReparsePoint);
        if (temporaryDirectoryIsReparsePoint) {
            throw new IOException("The temporary directory is a reparse point.");
        }

        var extension = SafeExtension(entryName);
        for (; ; ) {
            token.ThrowIfCancellationRequested();
            var directoryName = Guid.NewGuid().ToString("N");
            var directoryPath = Path.GetFullPath(Path.Combine(fullTemporaryDirectory, directoryName));
            EnsureContained(fullTemporaryDirectory, directoryPath);
            Directory.CreateDirectory(directoryPath);
            var directoryAttributes = File.GetAttributes(directoryPath);
            var directoryIsReparsePoint = directoryAttributes.HasFlag(FileAttributes.ReparsePoint);
            if (directoryIsReparsePoint) {
                throw new IOException("The temporary directory is a reparse point.");
            }

            var fileName = Guid.NewGuid().ToString("N") + extension;
            var filePath = Path.GetFullPath(Path.Combine(directoryPath, fileName));
            EnsureContained(directoryPath, filePath);
            try {
                using var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                return new FileInfo(filePath);
            }
            catch (IOException) {
                var fileExists = File.Exists(filePath) || Directory.Exists(filePath);
                if (fileExists is false) {
                    throw;
                }
            }
        }
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
        if (active.HasProvider(out ZipProvider provider) == false) {
            return false;
        }
        if (active.HasSelection<ZipEntry>(out var entries) == false) {
            return false;
        }
        return context.Operate(async (progress, token) => {
            var currentLocation = ArchiveProviderIDCodec.Parse(provider.Zip.FullName);
            async Task<bool> openFile(ZipEntryInfo info, CancellationToken token) {
                if (info.Name.Normalized.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
                    var id = ArchiveProviderIDCodec.Format(currentLocation.WithNestedMember(info.Name.Original));
                    var provided = await context.Provide(id, parameter.Where, token);
                    if (provided) {
                        return true;
                    }
                }
                var destination = await Task.Run(cancellationToken: token, function: () => {
                    var tempPath = Path.GetTempPath();
                    var tempDirectory = Path.Combine(tempPath, "Brows", nameof(ZipEntry));
                    return CreateTemporaryFile(tempDirectory, info.Name.Top, token);
                });
                await info.Extract(destination.FullName, overwrite: true, token);
                var extracted = await FileSystemTask.ExistingFile(destination.FullName, token);
                if (extracted != null) {
                    var service = OpenFile;
                    if (service != null) {
                        var open = await service.Work(extracted.FullName, token);
                        if (open) {
                            return true;
                        }
                    }
                }
                return false;
            }
            async Task<bool> openPath(ZipEntryInfo info, CancellationToken token) {
                var id = ArchiveProviderIDCodec.Format(currentLocation.WithDirectory(info.Name.Normalized));
                return await context.Provide(id, parameter.Where, token);
            }
            var worked = false;
            var selection = entries.Select(e => e.Info);
            foreach (var info in selection) {
                switch (info.Kind) {
                    case ZipEntryKind.File:
                        worked |= await openFile(info, token);
                        break;
                    case ZipEntryKind.Path:
                        worked |= await openPath(info, token);
                        break;
                }
            }
            return worked;
        });
    }

    public IOpenFile OpenFile { get; set; }

    public sealed class Parameter {
        [CliArgument]
        public CommandContextProvide Where { get; set; }
    }
}
