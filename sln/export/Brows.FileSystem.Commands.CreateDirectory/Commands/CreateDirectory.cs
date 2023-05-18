using Brows.Exports;
using Domore.IO;
using System;
using System.Collections.Generic;
using System.IO;

namespace Brows.Commands {
    internal sealed class CreateDirectory : FileSystemCommand<CreateDirectoryParameter> {
        protected sealed override IEnumerable<Type> Source { get; } = new[] {
            typeof(IEntry),
            typeof(IEntryObservation)
        };

        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == context.HasCommander(out var commander)) return false;
            if (false == context.HasParameter(out var parameter)) {
                if (false == context.HasGesture(out _)) {
                    return false;
                }
                var trigger = InputTrigger;
                var directoryName = "'New folder'";
                return context.ShowPalette($"{trigger} {directoryName}", trigger.Length + 2, directoryName.Length - 2);
            }
            if (false == context.HasSourceFileSystemDirectory(out var directory)) return false;
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
