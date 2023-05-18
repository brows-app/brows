using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Commands {
    internal sealed class Delete : ZipCommand<Delete.Parameter> {
        protected sealed override IEnumerable<Type> Source { get; } = new[] {
            typeof(IEntry)
        };

        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) {
                return false;
            }
            if (active.HasZipArchive(out var archive) == false) return false;
            if (active.HasZipSelection(out var selection) == false) {
                return false;
            }
            var entryNames = selection.Select(info => info.Name.Original).ToHashSet();
            return context.Operate(async (progress, cancellationToken) => {
                await archive.Update(
                    progress: progress,
                    token: cancellationToken,
                    info: new() {
                        DeleteEntries = entryNames
                    });
                return true;
            });
        }

        public class Parameter {
        }
    }
}
