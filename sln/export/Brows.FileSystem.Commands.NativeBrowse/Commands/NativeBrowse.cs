using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class NativeBrowse : FileSystemCommand<NativeBrowseParameter> {
        protected sealed override IEnumerable<Type> Source { get; } = new[] {
            typeof(IEntry),
            typeof(IEntryObservation)
        };

        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) return false;
            if (active.HasFileSystemDirectory(out var directory) == false) return false;
            var location = directory.FullName?.Trim() ?? "";
            if (location == "") {
                return false;
            }
            return context.Operate(async (operationProgress, cancellationToken) => {
                return await Task.Run(cancellationToken: cancellationToken, function: () => {
                    using (Process.Start("explorer.exe", location)) {
                        return true;
                    }
                });
            });
        }
    }
}
