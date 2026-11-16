using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class SftpMoveSource {
    private readonly SftpProvider Provider;
    private readonly IReadOnlyList<SftpEntry> Selection;
    private readonly IReadOnlyList<SftpEntry> Manifest;

    private SftpMoveSource(SftpProvider provider, IReadOnlyList<SftpEntry> selection,
        IReadOnlyList<SftpEntry> manifest) {
        Provider = provider;
        Selection = selection;
        Manifest = manifest;
    }

    private static void RequireSupportedKind(SftpEntry entry) {
        var supported = entry.Kind is FileProtocolEntryKind.File or FileProtocolEntryKind.Directory;
        if (!supported) {
            throw new NotSupportedException($"Copying {entry.Kind} entries is not supported.");
        }
    }

    private static async Task<SftpEntry> Refresh(SftpProvider provider, SftpEntry entry, CancellationToken token) {
        var parent = FileProtocolUri.Parent(entry.Uri);
        if (parent is not null) {
            await foreach (var current in provider.ListEntries(parent, token).WithCancellation(token)) {
                if (current.Uri == entry.Uri) {
                    return current;
                }
            }
        }
        throw new IOException("The SFTP source changed during the move; the source was retained.");
    }

    private static async Task<List<SftpEntry>> Capture(SftpProvider provider, IReadOnlyList<SftpEntry> selection,
        CancellationToken token) {
        var pending = new Stack<(SftpEntry Entry, bool DirectoryEnd)>();
        foreach (var entry in selection.Reverse()) {
            RequireSupportedKind(entry);
            pending.Push((await Refresh(provider, entry, token), false));
        }
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var manifest = new List<SftpEntry>();
        while (pending.TryPop(out var item)) {
            token.ThrowIfCancellationRequested();
            if (item.DirectoryEnd) {
                manifest.Add(item.Entry);
                continue;
            }
            var entry = item.Entry;
            if (!visited.Add(entry.Uri.AbsoluteUri)) {
                throw new InvalidDataException("The SFTP move selection contains a repeated path.");
            }
            RequireSupportedKind(entry);
            if (entry.Kind == FileProtocolEntryKind.File) {
                manifest.Add(entry);
                continue;
            }
            pending.Push((entry, true));
            await foreach (var child in provider.ListEntries(entry.Uri, token).WithCancellation(token)) {
                pending.Push((child, false));
            }
        }
        return manifest;
    }

    private static bool SameEntry(SftpEntry first, SftpEntry second) {
        var firstInfo = first.Info;
        var secondInfo = second.Info;
        var same = first.Uri == second.Uri && first.Kind == second.Kind && firstInfo.Flags == secondInfo.Flags &&
            ((firstInfo.Flags & 1) == 0 || firstInfo.Size == secondInfo.Size) &&
            ((firstInfo.Flags & 2) == 0 || (firstInfo.Uid == secondInfo.Uid && firstInfo.Gid == secondInfo.Gid)) &&
            ((firstInfo.Flags & 4) == 0 || firstInfo.Permissions == secondInfo.Permissions) &&
            ((firstInfo.Flags & 8) == 0 || firstInfo.Mtime == secondInfo.Mtime);
        return same;
    }

    internal static async Task<SftpMoveSource> Create(SftpProvider provider, IReadOnlyList<SftpEntry> selection,
        CancellationToken token) {
        var manifest = await Capture(provider, selection, token);
        return new SftpMoveSource(provider, selection, manifest);
    }

    internal async Task Remove(CancellationToken token) {
        var current = await Capture(Provider, Selection, token);
        var before = Manifest.OrderBy(entry => entry.Uri.AbsoluteUri, StringComparer.Ordinal).ToArray();
        var after = current.OrderBy(entry => entry.Uri.AbsoluteUri, StringComparer.Ordinal).ToArray();
        var same = before.Length == after.Length && before.Zip(after).All(pair => SameEntry(pair.First, pair.Second));
        if (!same) {
            throw new IOException("The SFTP source changed during the move; the source was retained.");
        }
        foreach (var entry in Manifest) {
            token.ThrowIfCancellationRequested();
            /*
             * Delete only copied paths. A final nonrecursive rmdir retains any
             * child that arrives after verification rather than traversing it.
             */
            await Provider.DeleteEntry(entry, token);
        }
    }
}
