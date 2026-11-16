using Brows.Shared.Archives;
using Domore.Conf.Cli;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Commands;

internal sealed class Open : TarCommand<Open.Parameter> {
    internal static string SafeExtension(string name) {
        var extension = Path.GetExtension(name);
        var maximumExtensionLength = 255 - 32;
        var hasInvalidCharacter = extension.Any(character =>
            char.IsControl(character) ||
            character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*');
        var extensionIsSafe = extension.Length > 1 &&
                              extension.Length <= maximumExtensionLength &&
                              hasInvalidCharacter is false;
        return extensionIsSafe ? extension : string.Empty;
    }

    protected sealed override IEnumerable<Type> Source { get; } = new[] {
        typeof(IEntry)
    };

    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (context.HasPanel(out var active) is false) {
            return false;
        }
        if (context.GetParameter(out var parameter) is false) {
            return false;
        }
        if (active.HasProvider(out TarProvider provider) is false) {
            return false;
        }
        if (active.HasSelection<TarEntry>(out var entries) is false) {
            return false;
        }
        return context.Operate(async (progress, token) => {
            var archiveLocation = new ArchiveProviderID(
                ArchiveProviderIDFormat.Tar,
                provider.Archive.File.FullName,
                provider.Archive.ArchivePath.Nest,
                "");

            async Task<bool> openFile(TarEntryInfo info, CancellationToken token) {
                /*
                 * A file that holds a tar archive is entered as a provider
                 * before the file is extracted to a temporary file, which is
                 * the same behavior that other archive providers have for
                 * their nested archives.
                 */
                var opensNestedArchive = info.Name.Normalized.EndsWith(".tar", StringComparison.OrdinalIgnoreCase);
                if (opensNestedArchive) {
                    var id = ArchiveProviderIDCodec.Format(archiveLocation.WithNestedMember(info.Name.Original));
                    var nested = await context.Provide(id, parameter.Where, token);
                    if (nested) {
                        return true;
                    }
                }
                var destination = await Task.Run(cancellationToken: token, function: () => {
                    var tempPath = Path.GetTempPath();
                    var extension = SafeExtension(info.Name.Top);
                    for (; ; ) {
                        var temporaryName = Guid.NewGuid().ToString("N") + extension;
                        var temporaryDirectory = Guid.NewGuid().ToString("N");
                        var tempFilePath = Path.Combine(
                            tempPath,
                            "Brows",
                            nameof(TarEntry),
                            temporaryDirectory,
                            temporaryName);
                        var tempFileInfo = new FileInfo(tempFilePath);
                        if (tempFileInfo.Exists is false) {
                            for (; ; ) {
                                var directory = Directory.CreateDirectory(tempFileInfo.DirectoryName);
                                if (directory.Exists) {
                                    break;
                                }
                                token.ThrowIfCancellationRequested();
                            }
                            return tempFileInfo;
                        }
                        token.ThrowIfCancellationRequested();
                    }
                });
                await info.Extract(destination.FullName, overwrite: false, token);
                var extracted = await FileSystemTask.ExistingFile(destination.FullName, token);
                if (extracted is not null) {
                    var service = OpenFile;
                    if (service is not null) {
                        var open = await service.Work(extracted.FullName, token);
                        if (open) {
                            return true;
                        }
                    }
                }
                return false;
            }

            async Task<bool> openPath(TarEntryInfo info, CancellationToken token) {
                var id = ArchiveProviderIDCodec.Format(archiveLocation.WithDirectory(info.Name.Normalized));
                return await context.Provide(id, parameter.Where, token);
            }

            var worked = false;
            var selection = entries.Select(e => e.Info);
            foreach (var info in selection) {
                switch (info.Kind) {
                    case TarEntryKind.File:
                        worked |= await openFile(info, token);
                        break;
                    case TarEntryKind.Path:
                        worked |= await openPath(info, token);
                        break;
                }
            }
            return worked;
        });
    }

    public IOpenFile OpenFile { get; set; }

    public sealed record Parameter {
        [CliArgument]
        public CommandContextProvide Where { get; init; }
    }
}
