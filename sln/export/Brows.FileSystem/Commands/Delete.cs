using Brows.Exports;
using Domore.Conf;
using System.Linq;

namespace Brows.Commands {
    internal sealed class Delete : FileSystemCommand<Delete.Parameter> {
        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) return false;
            if (context.GetParameter(out var parameter) == false) return false;
            if (active.HasFileSystemDirectory(out var directory) == false) return false;
            var service = DeleteFilesInDirectory;
            if (service == null) {
                return false;
            }
            if (active.HasSelection(out var entries) == false) {
                return false;
            }
            var names = entries
                .Select(entry => entry.Name)?
                .ToList();
            return context.Operate(async (operationProgress, cancellationToken) => {
                return await service.Work(names, directory.FullName, parameter, operationProgress, cancellationToken);
            });
        }

        public IDeleteFilesInDirectory DeleteFilesInDirectory { get; set; }

        public sealed class Parameter : IDeleteFilesInDirectoryOptions {
            [Conf("u")]
            public bool Unrecoverable { get; set; }
        }
    }
}
