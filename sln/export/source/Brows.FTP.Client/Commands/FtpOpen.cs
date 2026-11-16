using Brows.Composition;
using Domore.Conf.Cli;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Commands;

internal abstract class FtpOpen<TProvider> : FtpCommand<TProvider, FtpOpen<TProvider>.Parameter>
where TProvider : FtpProviderBase {
    private static void RemoveTemporaryFile(string path) {
        /*
         * Cleanup must not mask a transfer or opener exception. Try both operations independently because
         * resetting attributes can fail while deleting the owned file still works.
         */
        try {
            var fileExists = File.Exists(path);
            if (!fileExists) {
                return;
            }
            try {
                File.SetAttributes(path, FileAttributes.Normal);
            }
            catch {
            }
            try {
                File.Delete(path);
            }
            catch {
            }
        }
        catch {
        }
    }

    private static string SafeExtension(string name) {
        var extension = Path.GetExtension(name);
        var tooLong = extension.Length > 223;
        var hasInvalidCharacter = extension.Any(character => char.IsControl(character) ||
            character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*');
        var isSafe = extension.Length > 1 && !tooLong &&
                     !extension.EndsWith('.') && !extension.EndsWith(' ') && !hasInvalidCharacter;
        return isSafe ? extension : string.Empty;
    }

    private static string CreateTemporaryFile(string entryName, CancellationToken token) {
        var directory = Path.Combine(Path.GetTempPath(), "Brows", "Ftp");
        Directory.CreateDirectory(directory);
        var attributes = File.GetAttributes(directory);
        if (attributes.HasFlag(FileAttributes.ReparsePoint)) {
            throw new IOException("The FTP temporary directory is a reparse point.");
        }
        var extension = SafeExtension(entryName);
        for (;;) {
            token.ThrowIfCancellationRequested();
            var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + extension);
            try {
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                return path;
            }
            catch (IOException) when (File.Exists(path) || Directory.Exists(path)) {
            }
        }
    }

    protected sealed override IEnumerable<Type> Source { get; } = new[] {
        typeof(FtpEntry)
    };

    protected sealed override bool Work(Context context) {
        if (context is null || !context.HasPanel(out var panel) ||
            !context.GetParameter(out var parameter) ||
            !panel.HasProvider<TProvider>(out var provider) ||
            !context.HasSource(out FtpEntry _, out var entries)) {
            return false;
        }
        var where = parameter.Where ?? CommandContextProvide.ActivePanel;
        return context.Operate(async (progress, token) => {
            var worked = false;
            foreach (var entry in entries) {
                token.ThrowIfCancellationRequested();
                var isDirectory = entry.Kind is FileProtocolEntryKind.Directory or
                    FileProtocolEntryKind.DirectoryLink;
                var targetTypeIsUnknown = entry.Kind == FileProtocolEntryKind.FileLink;
                if (targetTypeIsUnknown) {
                    isDirectory = await provider.IsDirectoryTarget(entry, token);
                }
                if (isDirectory) {
                    var entryIsLink = entry.Kind is FileProtocolEntryKind.FileLink or
                        FileProtocolEntryKind.DirectoryLink;
                    var directoryID = entryIsLink ? entry.DirectoryUri.ToString() : entry.ID;
                    worked |= await context.Provide(directoryID, where, token);
                    continue;
                }
                var isFile = entry.Kind is FileProtocolEntryKind.File or FileProtocolEntryKind.FileLink;
                if (!isFile || OpenFile is null) {
                    continue;
                }
                var path = CreateTemporaryFile(entry.Name, token);
                try {
                    var length = entry.Kind == FileProtocolEntryKind.File ? entry.Info.Size : null;
                    using var source = await provider.Read(entry, length, token);
                    await using (var destination = new FileStream(
                        path,
                        FileMode.Truncate,
                        FileAccess.Write,
                        FileShare.Read)) {
                        await source.CopyToAsync(destination, token);
                    }
                    File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
                    worked |= await OpenFile.Work(path, token);
                }
                catch {
                    RemoveTemporaryFile(path);
                    throw;
                }
            }
            return worked;
        });
    }

    public IOpenFile OpenFile { get; set; }

    public sealed class Parameter {
        [CliArgument]
        public CommandContextProvide? Where { get; set; }
    }
}
