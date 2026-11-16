using Brows.Composition;
using Domore.Conf.Cli;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Commands;

internal sealed class Open : SftpCommand<Open.Parameter> {
    protected sealed override IEnumerable<Type> Source { get; } = [typeof(SftpEntry)];

    protected sealed override bool Work(Context context) {
        if (context is null || !context.HasPanel(out var active) || !context.HasSource(out SftpEntry _, out var entries)) {
            return false;
        }
        if (!context.GetParameter(out var parameter)) {
            return false;
        }
        var openFile = OpenFile;
        var where = parameter.Where ?? CommandContextProvide.ActivePanel;
        return context.Operate(async (_, token) => {
            var worked = false;
            foreach (var entry in entries) {
                if (entry.Kind is FileProtocolEntryKind.Directory or FileProtocolEntryKind.DirectoryLink) {
                    worked |= await context.Provide(entry.ID, where, token);
                    continue;
                }
                if (openFile is null) {
                    continue;
                }
                var directory = Path.Combine(Path.GetTempPath(), "Brows", "SFTP", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                var name = SafeName(entry.Name);
                var path = Path.Combine(directory, name);
                try {
                    await using (var remote = await entry.Client.OpenRead(entry.RemotePath, token))
                    await using (var local = new FileStream(path,
                                                            FileMode.CreateNew,
                                                            FileAccess.Write,
                                                            FileShare.Read,
                                                            81920,
                                                            FileOptions.Asynchronous | FileOptions.SequentialScan)) {
                        await remote.CopyToAsync(local, token);
                    }
                    File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
                    worked |= await openFile.Work(path, token);
                }
                catch {
                    if (File.Exists(path)) {
                        File.SetAttributes(path, FileAttributes.Normal);
                        File.Delete(path);
                    }
                    if (Directory.Exists(directory)) {
                        Directory.Delete(directory);
                    }
                    throw;
                }
            }
            return worked;
        });
    }

    private static string SafeName(string name) {
        var invalid = Path.GetInvalidFileNameChars();
        var characters = name.ToCharArray();
        for (var index = 0; index < characters.Length; index++) {
            if (characters[index] is '/' or '\\' || Array.IndexOf(invalid, characters[index]) >= 0) {
                characters[index] = '_';
            }
        }
        return characters.Length == 0 ? "download" : new string(characters);
    }

    public IOpenFile OpenFile { get; set; }

    public sealed class Parameter {
        [CliArgument]
        public CommandContextProvide? Where { get; set; }
    }
}
