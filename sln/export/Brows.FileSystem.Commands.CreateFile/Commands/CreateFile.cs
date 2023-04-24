using Brows.Exports;
using Domore.IO;
using System.IO;

namespace Brows.Commands {
    internal class CreateFile : FileSystemCommand<CreateFileParameter> {
        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasCommander(out var commander) == false) return false;
            if (context.HasGesture(out _)) {
                var trigger = InputTrigger();
                var fileName = "'New file'";
                return context.Operate(async (progress, token) => {
                    return await commander.ShowPalette($"{trigger} {fileName}", trigger.Length + 1, fileName.Length, token);
                });
            }
            if (context.HasParameter(out var parameter)) {
                var name = parameter.Name?.Trim() ?? "";
                if (name == "") {
                    return false;
                }
                if (context.HasPanel(out var active) == false) {
                    return false;
                }
                if (active.HasFileSystemDirectory(out var directory) == false) {
                    return false;
                }
                var service = CreateDirectoryInfoFile;
                if (service == null) {
                    return false;
                }
                return context.Operate(async (progress, token) => {
                    var serviced = await service.Work(directory, name, progress, token);
                    if (serviced) {
                        var open = parameter.Open;
                        if (open == true) {
                            var path = Path.Combine(directory.FullName, name);
                            var existing = await FileSystemTask.ExistingFile(path, token);
                            if (existing != null) {
                                var service = OpenFile;
                                if (service != null) {
                                    await service.Work(existing.FullName, token);
                                }
                            }
                        }
                        return true;
                    }
                    return false;
                });
            }
            return false;
        }

        public IOpenFile OpenFile { get; set; }
        public ICreateDirectoryInfoFile CreateDirectoryInfoFile { get; set; }
    }
}
