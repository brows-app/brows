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
            if (active.HasFileSystemSelection(out var selection) == false) return false;
            return context.Operate(async (progress, token) => {
                switch (parameter.Mode) {
                    case null:
                    case Mode.Native:
                    default:
                        var service = NativeService;
                        if (service == null) {
                            return false;
                        }
                        var names = selection.Select(info => info.Name).ToList();
                        return await service.Work(names, directory.FullName, parameter, progress, token);
                    case Mode.Managed:
                        var prog = new FileSystemOperationProgress(progress);
                        var tasks = selection.Select(async info => await FileSystemTask.Delete(info, prog, token)).ToList();
                        await Task.WhenAll(tasks);
                        return tasks.Count > 0;
                }
            });
        }

        public IDeleteFilesInDirectory NativeService { get; set; }

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
