using Brows.Exports;
using Domore.Conf;
using Domore.Conf.Cli;
using Domore.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class Delete : FileSystemCommand<Delete.Parameter> {
        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) return false;
            if (context.GetParameter(out var parameter) == false) return false;
            if (active.HasFileSystemDirectory(out var directory) == false) return false;
            if (active.HasSelection(out var entries) == false) {
                return false;
            }
            return context.Operate(async (progress, token) => {
                switch (parameter.Mode) {
                    case null:
                    case Mode.Native:
                        goto default;
                    case Mode.Managed:
                        await Task.WhenAll(entries
                            .OfType<FileSystemEntry>()
                            .Select(e => FileSystemTask.Delete(e.Info, new FileSystemOperationProgress(progress), token)));
                        return true;
                    default:
                        var service = DeleteFilesInDirectory;
                        if (service == null) {
                            return false;
                        }
                        var names = entries.Select(entry => entry.Name).ToList();
                        return await service.Work(names, directory.FullName, parameter, progress, token);
                }
            });
        }

        public IDeleteFilesInDirectory DeleteFilesInDirectory { get; set; }

        public enum Mode {
            [Conf("n", "native")]
            [CliDisplayOverride("(n)ative")]
            Native,

            [Conf("m", "managed")]
            [CliDisplayOverride("(m)anaged")]
            Managed
        }

        public sealed class Parameter : IDeleteFilesInDirectoryOptions {
            [Conf("u")]
            public bool? Unrecoverable { get; set; }
            public Mode? Mode { get; set; }
        }
    }
}
