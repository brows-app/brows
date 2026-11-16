using Brows.Composition;
using Brows.Panels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Brows.Commands;

internal sealed class Move : Command<WorkIOParameter> {
    protected sealed override Type Provider => typeof(SftpProvider);
    protected sealed override IEnumerable<Type> Source { get; } = [typeof(SftpEntry)];

    protected sealed override bool Work(Context context) {
        if (context is null || !context.HasSource(out var commandSource) || !context.HasSource(out SftpEntry _, out var entries)) {
            return false;
        }
        if (!context.HasParameter(out var parameter)) {
            return false;
        }
        var panelMode = parameter.Where switch {
            WorkIOWhere.Auto => PanelPassiveMode.Auto,
            WorkIOWhere.Active => PanelPassiveMode.Active,
            WorkIOWhere.Next => PanelPassiveMode.Next,
            WorkIOWhere.None => PanelPassiveMode.None,
            WorkIOWhere.Previous => PanelPassiveMode.Previous,
            _ => throw new ArgumentOutOfRangeException(nameof(parameter.Where))
        };
        if (!context.HasPanels(panelMode, out var active, out var passive)) {
            return false;
        }
        if (!active.HasProviderService(out var activeProvider, out IProvideIO provide)) {
            return false;
        }
        if (activeProvider is not SftpProvider sourceProvider || !passive.HasProviderService(out var target, out ICopyProvidedIO copy)) {
            return false;
        }
        var selected = entries.OfType<SftpEntry>().DistinctBy(entry => entry.ID).ToList();
        if (selected.Count == 0) {
            return false;
        }
        return context.Operate(async (progress, token) => {
            if (target is SftpProvider destination && ReferenceEquals(sourceProvider.Client, destination.Client)) {
                foreach (var entry in selected) {
                    token.ThrowIfCancellationRequested();
                    var destinationPath = FileProtocolUri.CombinePath(destination.Uri, entry.Name);
                    var sourcePath = entry.MutationPath;
                    if (entry.Kind == FileProtocolEntryKind.Directory &&
                        destinationPath.StartsWith(sourcePath + "/", StringComparison.Ordinal)) {
                        throw new IOException("A directory cannot be moved into itself.");
                    }
                    if (!string.Equals(sourcePath, destinationPath, StringComparison.Ordinal)) {
                        await sourceProvider.RenameToPath(entry, destinationPath, token);
                    }
                }
                await new ProviderPostponedRefresh(sourceProvider).Work(final: true, token);
                await new ProviderPostponedRefresh(destination).Work(final: true, token);
                return true;
            }

            var io = new List<IProvidedIO>();
            if (!await provide.Work(io, commandSource, activeProvider, progress, token)) {
                return false;
            }
            var source = await SftpMoveSource.Create(sourceProvider, selected, token);
            if (!await copy.Work(io, target, progress, token)) {
                return false;
            }
            await source.Remove(token);
            await new ProviderPostponedRefresh(sourceProvider).Work(final: true, token);
            return true;
        });
    }
}
