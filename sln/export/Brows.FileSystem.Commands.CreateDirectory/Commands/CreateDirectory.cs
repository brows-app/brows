using Brows.Exports;
using Domore.IO;
using System.IO;

namespace Brows.Commands {
    internal sealed class CreateDirectory : FileSystemCommand<CreateDirectoryParameter> {
        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) return false;
            if (context.HasCommander(out var commander) == false) return false;
            if (context.HasGesture(out _)) {
                var trigger = InputTrigger;
                var directoryName = "'New folder'";
                return context.Operate(async (progress, token) => {
                    return await commander.ShowPalette($"{trigger} {directoryName}", trigger.Length + 1, directoryName.Length, token);
                });
            }
            if (context.HasParameter(out var parameter) == false) return false;
            if (active.HasFileSystemDirectory(out var directory) == false) {
                return false;
            }
            var service = CreateDirectoryInfoDirectory;
            if (service == null) {
                return false;
            }
            var open = parameter.Open;
            var name = parameter.Name?.Trim() ?? "";
            if (name == "") {
                return false;
            }
            return context.Operate(async (progress, token) => {
                var serviced = await service.Work(directory, name, progress, token);
                if (serviced) {
                    if (open.HasValue) {
                        var path = Path.Join(directory.FullName, name);
                        var existing = await FileSystemTask.ExistingDirectory(path, token);
                        if (existing != null) {
                            await context.Provide(existing.FullName, open.Value, token);
                        }
                    }
                    return true;
                }
                return false;
            });
        }

        public ICreateDirectoryInfoDirectory CreateDirectoryInfoDirectory { get; set; }
    }
}
