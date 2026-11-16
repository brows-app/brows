using Brows.Shared.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows;

internal sealed class TarID {
    private static bool IsDirectory(TarEntryName path, IEnumerable<TarEntryInfo> entries) {
        var directoryName = path.Normalized;
        var directoryPrefix = directoryName + Path.DirectorySeparatorChar;
        return entries.Any(info => {
            var isExplicitDirectory = info.Kind == TarEntryKind.Path &&
                                      string.Equals(info.Name.Normalized, directoryName, StringComparison.Ordinal);
            var containsChild = info.Name.Normalized.StartsWith(directoryPrefix, StringComparison.Ordinal);
            return isExplicitDirectory || containsChild;
        });
    }

    private static async Task<TarEntryInfo> FindNestedMember(
        TarArchiveIdentity identity,
        IReadOnlyList<string> nesting,
        string member,
        CancellationToken token) {
        try {
            var archivePath = TarArchivePath.Get(identity, new TarArchiveNest(nesting));
            try {
                var entries = await archivePath.EntryInfo(token);
                var memberName = new TarEntryName(member).Normalized;
                return entries.Items.FirstOrDefault(info => {
                    var isFile = info.Kind == TarEntryKind.File;
                    var hasMatchingName = string.Equals(info.Name.Normalized, memberName, StringComparison.Ordinal);
                    return isFile && hasMatchingName;
                });
            }
            finally {
                archivePath.Release();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            /*
             * An entry that cannot be probed is not a usable nested archive member.
             */
            return null;
        }
    }

    private static ArchiveProviderID Location(
        TarArchivePath archivePath,
        string directory) {
        return new ArchiveProviderID(
            ArchiveProviderIDFormat.Tar,
            archivePath.FileName,
            archivePath.Nest,
            directory);
    }

    private static string FormatLocation(TarArchivePath archivePath, string directory) {
        return ArchiveProviderIDCodec.Format(Location(archivePath, directory));
    }

    private TarID(TarArchivePath archivePath, TarEntryName relativePath) {
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        ArchivePath = archivePath ?? throw new ArgumentNullException(nameof(archivePath));
    }

    public TarArchiveFileEvent FileChanged => ArchivePath.FileChanged;
    public TarArchiveFileEvent FileDeleted => ArchivePath.FileDeleted;

    public object Icon { get; private set; }
    public FileInfo File => ArchivePath.File;

    public string Parent {
        get {
            if (field is not null) {
                return field;
            }
            if (RelativePath.Parts.Count > 0) {
                field = FormatLocation(ArchivePath, RelativePath.Parent);
                return field;
            }
            if (ArchivePath.Nest.Count == 0) {
                field = ArchivePath.Parent;
                return field;
            }

            var nesting = ArchivePath.Nest.ToList();
            var lastMember = nesting[^1];
            nesting.RemoveAt(nesting.Count - 1);
            var parentDirectory = new TarEntryName(lastMember).Parent;
            var parentLocation = new ArchiveProviderID(
                ArchiveProviderIDFormat.Tar,
                ArchivePath.FileName,
                nesting,
                parentDirectory);
            field = ArchiveProviderIDCodec.Format(parentLocation);
            return field;
        }
    }

    public string FullName => field ??= FormatLocation(ArchivePath, RelativePath.Normalized);

    public TarArchivePath ArchivePath { get; }
    public TarEntryName RelativePath { get; }

    public void Release() {
        ArchivePath.Release();
    }

    public async Task<IReadOnlyList<TarEntryInfo>> EntryInfo(CancellationToken cancellationToken) {
        var set = await ArchivePath.EntryInfo(cancellationToken);
        return set.Items
            .Where(info => info.Name.Parent == RelativePath.Normalized)
            .ToList();
    }

    public static async Task<TarID> Parse(string id, TarProviderFactory factory, CancellationToken token) {
        if (factory is null) {
            throw new ArgumentNullException(nameof(factory));
        }
        if (string.IsNullOrEmpty(id)) {
            return null;
        }

        ArchiveProviderID location;
        var hasArchiveProviderPrefix = ArchiveProviderIDCodec.HasArchiveProviderPrefix(id);
        if (hasArchiveProviderPrefix) {
            var providerIDIsValid = ArchiveProviderIDCodec.TryParse(id, out location);
            var providerIDIsTar = providerIDIsValid && location.Format == ArchiveProviderIDFormat.Tar;
            if (providerIDIsTar is false) {
                return null;
            }
        }
        else {
            location = new ArchiveProviderID(ArchiveProviderIDFormat.Tar, id, Array.Empty<string>(), "");
        }

        var hasTarExtension = Path.GetExtension(location.FileName).Equals(
            ".tar",
            StringComparison.OrdinalIgnoreCase);
        if (hasTarExtension is false) {
            return null;
        }
        var fileNameIsAbsolute = Path.IsPathFullyQualified(location.FileName);
        if (fileNameIsAbsolute is false) {
            return null;
        }
        var file = new FileInfo(location.FileName);
        var fileExists = await Task.Run(cancellationToken: token, function: () => file.Exists);
        if (fileExists is false) {
            return null;
        }

        var identity = await TarArchiveIdentity.Create(
            file,
            factory.CaseCorrectFile,
            factory.CaseSensitiveDirectory,
            token);
        file = new FileInfo(identity.FullName);

        var nesting = new List<string>(location.NestedMembers.Count);
        foreach (var nestedMember in location.NestedMembers) {
            token.ThrowIfCancellationRequested();
            var matchingMember = await FindNestedMember(identity, nesting, nestedMember, token);
            if (matchingMember is null) {
                return null;
            }
            nesting.Add(matchingMember.Name.Original);
        }

        var archivePath = TarArchivePath.Get(identity, new TarArchiveNest(nesting));
        var relativePath = new TarEntryName(location.Directory);
        var keepArchivePath = false;
        try {
            if (relativePath.Parts.Count > 0) {
                var entries = await archivePath.EntryInfo(token);
                if (IsDirectory(relativePath, entries.Items) is false) {
                    return null;
                }
            }

            var icon = default(object);
            var iconService = factory.IconTarFileInfo;
            if (iconService is not null) {
                icon = await iconService.Icon(file, token);
            }
            var parsedID = new TarID(archivePath, relativePath) {
                Icon = icon
            };
            keepArchivePath = true;
            return parsedID;
        }
        finally {
            if (keepArchivePath is false) {
                archivePath.Release();
            }
        }
    }
}
