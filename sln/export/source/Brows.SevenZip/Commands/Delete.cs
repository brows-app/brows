using Brows.Entries;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Commands;

internal sealed class Delete : SevenZipCommand<Delete.Parameter> {
    protected sealed override IEnumerable<Type> Source { get; } = new[] {
        typeof(IEntry)
    };

    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (context.HasPanel(out var active) is false) {
            return false;
        }
        if (active.HasSevenZipArchive(out var archive) is false) {
            return false;
        }
        if (active.HasSevenZipSelection(out var selection) is false) {
            return false;
        }
        var entryNames = selection.Select(info => info.Name.Original).ToHashSet();
        return context.Operate(async (progress, cancellationToken) => {
            await archive.Update(
                progress: progress,
                token: cancellationToken,
                info: new SevenZipArchiveUpdate {
                    DeleteEntries = entryNames
                });
            return true;
        });
    }

    internal sealed record Parameter;
}
