using Brows.Exports;
using Domore.IO;
using System;
using System.Collections.Generic;
using System.IO;

namespace Brows.Commands {
    internal class CreateFile : FileSystemCommand<CreateFileParameter> {
        protected sealed override IEnumerable<Type> Source { get; } = new[] {
            typeof(IEntry),
            typeof(IEntryObservation)
        };

        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasCommander(out var commander)) return false;
            if (false == context.HasParameter(out var parameter)) {
                if (false == context.HasGesture(out _)) {
                    return false;
                }
                var trigger = InputTrigger;
                var fileName = "'New file'";
                return context.ShowPalette($"{trigger} {fileName}", trigger.Length + 2, fileName.Length - 2);
            }
            if (false == context.HasSourceFileSystemDirectory(out var directory)) {
                return false;
            }
            var name = parameter.Name?.Trim() ?? "";
            if (name == "") {
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

        public IOpenFile OpenFile { get; set; }
        public ICreateDirectoryInfoFile CreateDirectoryInfoFile { get; set; }
    }
}
