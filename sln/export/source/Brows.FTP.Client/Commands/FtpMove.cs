using Brows.Composition;
using Brows.Panels;
using Domore.Conf.Cli;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

internal abstract class FtpMove<TProvider> : FtpCommand<TProvider, FtpMove<TProvider>.Parameter>
where TProvider : FtpProviderBase {
    private static readonly TimeSpan FailedOperationRefreshTimeout = TimeSpan.FromSeconds(5);

    private sealed record MoveItem(FtpEntry Entry, string DestinationPath, bool IsNoOp);

    private sealed record DeleteItem(FtpEntry Entry, bool IsDirectory);

    private static bool HasServerAbsolutePath(Uri uri) {
        return uri.AbsolutePath.StartsWith("//", StringComparison.Ordinal);
    }

    private static bool HasSamePathMode(Uri first, Uri second) {
        return HasServerAbsolutePath(first) == HasServerAbsolutePath(second);
    }

    private static bool IsFtpScheme(string scheme) {
        return scheme.Equals("ftp", StringComparison.OrdinalIgnoreCase) ||
               scheme.Equals("ftpes", StringComparison.OrdinalIgnoreCase) ||
               scheme.Equals("ftps", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameFtpEndpoint(Uri first, Uri second) {
        var bothUseFtp = IsFtpScheme(first.Scheme) && IsFtpScheme(second.Scheme);
        if (!bothUseFtp) {
            return false;
        }
        return string.Equals(first.IdnHost, second.IdnHost, StringComparison.OrdinalIgnoreCase) &&
               first.Port == second.Port;
    }

    private static bool SameRenameProfile(
        FtpProviderBase source,
        Uri sourceUri,
        FtpProviderBase destination,
        Uri destinationUri) {
        var sameEndpoint = SameFtpEndpoint(sourceUri, destinationUri);
        return sameEndpoint && source.HasSameRenameProfile(destination, sourceUri, destinationUri);
    }

    private static bool SamePath(Uri first, Uri second) {
        return string.Equals(first.AbsolutePath.TrimEnd('/'),
                             second.AbsolutePath.TrimEnd('/'),
                             StringComparison.Ordinal);
    }

    private static bool IsSameOrDescendantPath(Uri ancestor, Uri descendant) {
        var ancestorPath = ancestor.AbsolutePath.TrimEnd('/');
        var descendantPath = descendant.AbsolutePath.TrimEnd('/');
        var samePath = string.Equals(ancestorPath, descendantPath, StringComparison.Ordinal);
        if (samePath) {
            return true;
        }
        var prefix = ancestorPath.Length == 0
            ? "/"
            : ancestorPath + "/";
        return descendantPath.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static Uri Root(Uri uri, bool serverAbsolute) {
        var path = serverAbsolute ? "//" : "/";
        return new UriBuilder(uri) {
            Path = path,
            Query = "",
            Fragment = ""
        }.Uri;
    }

    private static Uri Extend(Uri uri, string part, bool includeTrailingSlash) {
        var extended = FileProtocolUri.Extend(uri, [part], includeTrailingSlash);
        var serverRootWasLost = HasServerAbsolutePath(uri) && !HasServerAbsolutePath(extended);
        if (!serverRootWasLost) {
            return extended;
        }
        var path = "//" + extended.AbsolutePath.TrimStart('/');
        return new UriBuilder(extended) { Path = path }.Uri;
    }

    private static Uri ResolveDestination(Uri baseUri, string destination) {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        var isRooted = destination.StartsWith("/", StringComparison.Ordinal);
        var startsAtServerRoot = destination.StartsWith("//", StringComparison.Ordinal);
        var current = isRooted
            ? Root(baseUri, startsAtServerRoot)
            : new UriBuilder(baseUri) { Query = "", Fragment = "" }.Uri;
        var root = Root(current, HasServerAbsolutePath(current));
        var parts = destination.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts) {
            if (part == ".") {
                continue;
            }
            if (part == "..") {
                var parent = FileProtocolUri.Parent(current);
                var parentIsOutsideRoot = parent is null || !HasSamePathMode(root, parent);
                if (parentIsOutsideRoot) {
                    throw new ArgumentException("The FTP destination cannot move above its root.", nameof(destination));
                }
                current = parent;
                continue;
            }
            current = Extend(current, part, includeTrailingSlash: true);
        }
        return current;
    }

    private static string RemotePath(Uri uri) {
        _ = FileProtocolUri.CombinePath(uri, "brows-move-validation");
        var path = Uri.UnescapeDataString(uri.AbsolutePath).TrimEnd('/');
        if (string.IsNullOrEmpty(path)) {
            throw new ArgumentException("The FTP destination must identify an entry.", nameof(uri));
        }
        return path;
    }

    private static List<MoveItem> MoveItems(
        TProvider source,
        Uri destination,
        IReadOnlyList<FtpEntry> entries) {
        var moveItems = new List<MoveItem>();
        foreach (var entry in entries) {
            var destinationUri = Extend(
                destination,
                entry.Name,
                includeTrailingSlash: entry.Kind == FileProtocolEntryKind.Directory);
            var directoryWouldMoveIntoItself = entry.Kind == FileProtocolEntryKind.Directory &&
                                               SameFtpEndpoint(source.Uri, destinationUri) &&
                                               HasSamePathMode(entry.Uri, destinationUri) &&
                                               IsSameOrDescendantPath(entry.Uri, destination);
            if (directoryWouldMoveIntoItself) {
                throw new IOException("A directory cannot be moved into itself.");
            }
            var noOp = SamePath(entry.Uri, destinationUri);
            moveItems.Add(new MoveItem(entry, RemotePath(destinationUri), noOp));
        }
        return moveItems;
    }

    private static void EnsureNoOverlappingPaths(Uri destination, IReadOnlyList<FtpEntry> entries) {
        foreach (var entry in entries) {
            var destinationUri = Extend(
                destination,
                entry.Name,
                includeTrailingSlash: entry.Kind == FileProtocolEntryKind.Directory);
            if (SamePath(entry.Uri, destinationUri)) {
                throw new IOException("The FTP move source and destination paths are the same.");
            }
            var destinationIsWithinSourceDirectory = entry.Kind == FileProtocolEntryKind.Directory &&
                                                     IsSameOrDescendantPath(entry.Uri, destination);
            if (destinationIsWithinSourceDirectory) {
                throw new IOException("A directory cannot be moved into itself.");
            }
        }
    }

    private static async Task<FtpEntry> RefreshSelectedEntry(
        TProvider provider,
        FtpEntry entry,
        CancellationToken token) {
        token.ThrowIfCancellationRequested();
        var parent = FileProtocolUri.Parent(entry.Uri);
        if (parent is null) {
            throw new IOException("The FTP source changed during the move; the source was retained.");
        }
        await foreach (var current in provider.ListEntries(parent, token).WithCancellation(token)) {
            var sameEntry = current.Uri == entry.Uri;
            if (sameEntry) {
                return current;
            }
        }
        throw new IOException("The FTP source changed during the move; the source was retained.");
    }

    private static async Task<List<DeleteItem>> SourceRemovalPlan(
        TProvider provider,
        IReadOnlyList<FtpEntry> entries,
        CancellationToken token) {
        var plan = new List<DeleteItem>();
        var pending = new Stack<(FtpEntry Entry, bool IsDirectoryEnd)>();
        for (var i = entries.Count - 1; i >= 0; i--) {
            var current = await RefreshSelectedEntry(provider, entries[i], token);
            pending.Push((current, false));
        }
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (pending.Count > 0) {
            token.ThrowIfCancellationRequested();
            var (entry, isDirectoryEnd) = pending.Pop();
            if (isDirectoryEnd) {
                plan.Add(new DeleteItem(entry, IsDirectory: true));
                continue;
            }
            var added = visited.Add(entry.Uri.AbsoluteUri);
            if (!added) {
                throw new InvalidDataException("The FTP move selection contains a repeated path.");
            }
            if (entry.Kind == FileProtocolEntryKind.File) {
                plan.Add(new DeleteItem(entry, IsDirectory: false));
                continue;
            }
            if (entry.Kind is FileProtocolEntryKind.FileLink or FileProtocolEntryKind.DirectoryLink) {
                throw new NotSupportedException($"Moving {entry.Kind} entries across providers is not supported.");
            }
            if (entry.Kind != FileProtocolEntryKind.Directory) {
                throw new NotSupportedException($"Moving {entry.Kind} entries across providers is not supported.");
            }

            pending.Push((entry, true));
            var children = new List<FtpEntry>();
            await foreach (var child in provider.ListEntries(entry.Uri, token).WithCancellation(token)) {
                children.Add(child);
            }
            for (var i = children.Count - 1; i >= 0; i--) {
                pending.Push((children[i], false));
            }
        }
        return plan;
    }

    private static bool SameManifest(IReadOnlyList<DeleteItem> first, IReadOnlyList<DeleteItem> second) {
        var firstPaths = first
            .Select(item => (
                item.Entry.Uri.AbsoluteUri,
                item.Entry.Kind,
                item.Entry.Info.Size,
                item.Entry.Info.Time))
            .OrderBy(item => item.AbsoluteUri, StringComparer.Ordinal);
        var secondPaths = second
            .Select(item => (
                item.Entry.Uri.AbsoluteUri,
                item.Entry.Kind,
                item.Entry.Info.Size,
                item.Entry.Info.Time))
            .OrderBy(item => item.AbsoluteUri, StringComparer.Ordinal);
        return firstPaths.SequenceEqual(secondPaths);
    }

    private static async Task DeleteSource(TProvider provider,
                                           IReadOnlyList<DeleteItem> plan,
                                           Action deleteAttempted,
                                           CancellationToken token) {
        token.ThrowIfCancellationRequested();
        foreach (var item in plan) {
            token.ThrowIfCancellationRequested();
            deleteAttempted();
            await provider.DeleteEntry(item.Entry, item.IsDirectory, token);
        }
    }

    private static async Task RefreshAfterFailure(
        IEnumerable<IProvider> providers,
        Func<IProvider, CancellationToken, Task> refresh) {
        using var timeout = new CancellationTokenSource(FailedOperationRefreshTimeout);
        var refreshed = new HashSet<IProvider>(ReferenceEqualityComparer.Instance);
        foreach (var provider in providers) {
            var isNewProvider = provider is not null && refreshed.Add(provider);
            if (!isNewProvider) {
                continue;
            }
            try {
                await refresh(provider, timeout.Token);
            }
            catch (Exception) {
            }
        }
    }

    private async Task<bool> RenameEntries(
        TProvider source,
        IProvider destination,
        IReadOnlyList<MoveItem> items,
        CancellationToken token) {
        var renameMayHaveOccurred = false;
        var completed = false;
        try {
            foreach (var item in items) {
                token.ThrowIfCancellationRequested();
                if (item.IsNoOp) {
                    continue;
                }
                renameMayHaveOccurred = true;
                await Rename(source, item.Entry, item.DestinationPath, token);
            }
            await RefreshProvider(source, token);
            if (!ReferenceEquals(source, destination)) {
                await RefreshProvider(destination, token);
            }
            completed = true;
            return true;
        }
        finally {
            var failedAfterRenameAttempt = !completed && renameMayHaveOccurred;
            if (failedAfterRenameAttempt) {
                await RefreshAfterFailure(
                    [source, destination],
                    (provider, refreshToken) => RefreshProvider(provider, refreshToken));
            }
        }
    }

    protected sealed override bool Work(Context context) {
        if (context is null ||
            !context.GetParameter(out var parameter) ||
            !context.HasSource(out var commandSource) ||
            !context.HasSource(out FtpEntry _, out var selectedEntries)) {
            return false;
        }
        var entries = selectedEntries
            .DistinctBy(entry => entry.ID)
            .ToList();
        if (entries.Count == 0) {
            return false;
        }

        if (context.HasPanel(out var currentPanel) &&
            currentPanel.HasProvider<TProvider>(out var currentProvider) &&
            !string.IsNullOrWhiteSpace(parameter.Destination)) {
            var destination = ResolveDestination(currentProvider.Uri, parameter.Destination);
            var explicitDestinationHasSameEndpoint = SameFtpEndpoint(currentProvider.Uri, destination);
            if (explicitDestinationHasSameEndpoint && !HasSamePathMode(currentProvider.Uri, destination)) {
                throw new NotSupportedException(
                    "FTP moves between login-relative and server-absolute paths are not supported.");
            }
            var items = MoveItems(currentProvider, destination, entries);
            return context.Operate((progress, token) => RenameEntries(
                currentProvider,
                currentProvider,
                items,
                token));
        }

        if (!context.HasPanels(PanelPassiveMode.Auto, out var active, out var passive) ||
            !active.HasProvider<TProvider>(out var sourceProvider) ||
            !active.HasProviderService(out var activeProvider, out IProvideIO provide) ||
            !passive.HasProviderService(out var target, out ICopyProvidedIO copy)) {
            return false;
        }

        var activeIsSourceProvider = ReferenceEquals(activeProvider, sourceProvider);
        if (!activeIsSourceProvider) {
            return false;
        }
        var destinationProvider = target as FtpProviderBase;
        var sameFtpEndpoint = destinationProvider is not null &&
                              SameFtpEndpoint(sourceProvider.Uri, destinationProvider.Uri);
        var sameEndpointHasDifferentPathModes = sameFtpEndpoint &&
                                                !HasSamePathMode(sourceProvider.Uri, destinationProvider.Uri);
        if (sameEndpointHasDifferentPathModes) {
            throw new NotSupportedException(
                "FTP moves between login-relative and server-absolute paths are not supported.");
        }

        var sameRenameProfile = destinationProvider is not null &&
                                HasSamePathMode(sourceProvider.Uri, destinationProvider.Uri) &&
                                SameRenameProfile(
                                    sourceProvider,
                                    sourceProvider.Uri,
                                    destinationProvider,
                                    destinationProvider.Uri);
        if (sameRenameProfile) {
            var items = MoveItems(sourceProvider, destinationProvider.Uri, entries);
            return context.Operate((progress, token) => RenameEntries(
                sourceProvider,
                destinationProvider,
                items,
                token));
        }

        var sameEndpointHasDifferentRenameProfiles = sameFtpEndpoint && !sameRenameProfile;
        if (destinationProvider is not null && sameEndpointHasDifferentRenameProfiles) {
            EnsureNoOverlappingPaths(destinationProvider.Uri, entries);
        }

        return context.Operate(async (progress, token) => {
            var targetMayHaveChanged = false;
            var sourceMayHaveChanged = false;
            var completed = false;
            try {
                var removalPlan = await SourceRemovalPlan(sourceProvider, entries, token);
                var io = new List<IProvidedIO>();
                var ioWasProvided = await provide.Work(io, commandSource, sourceProvider, progress, token);
                if (!ioWasProvided) {
                    return false;
                }
                var providedIO = io.ToList();
                targetMayHaveChanged = true;
                var copied = await copy.Work(providedIO, target, progress, token);
                if (!copied) {
                    return false;
                }

                token.ThrowIfCancellationRequested();
                var currentRemovalPlan = await SourceRemovalPlan(sourceProvider, entries, token);
                if (!SameManifest(removalPlan, currentRemovalPlan)) {
                    throw new IOException("The FTP source listing changed during the move; the source was retained.");
                }
                await DeleteSource(
                    sourceProvider,
                    removalPlan,
                    () => sourceMayHaveChanged = true,
                    token);
                await RefreshProvider(sourceProvider, token);
                await RefreshProvider(target, token);
                completed = true;
                return true;
            }
            finally {
                var failedAfterPossibleMutation = !completed && (sourceMayHaveChanged || targetMayHaveChanged);
                if (failedAfterPossibleMutation) {
                    var providers = new List<IProvider>();
                    if (sourceMayHaveChanged) {
                        providers.Add(sourceProvider);
                    }
                    if (targetMayHaveChanged) {
                        providers.Add(target);
                    }
                    await RefreshAfterFailure(
                        providers,
                        (provider, refreshToken) => RefreshProvider(provider, refreshToken));
                }
            }
        });
    }

    protected virtual Task RefreshProvider(IProvider provider, CancellationToken token) {
        return ((IProvider)provider).Refresh(token);
    }

    protected virtual Task Rename(TProvider provider,
                                  FtpEntry entry,
                                  string destinationPath,
                                  CancellationToken token) {
        return provider.ClientFor(entry).Rename(entry.RemotePath, destinationPath, token);
    }

    public sealed class Parameter {
        [CliArgument]
        public string Destination { get; set; }
    }
}
