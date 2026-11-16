using Brows.Shared.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows;

internal sealed class SevenZipID {
    private static bool IsDirectory(SevenZipEntryName path, IEnumerable<SevenZipEntryInfo> entries) {
        var directoryName = path.Normalized;
        var directoryPrefix = directoryName + Path.DirectorySeparatorChar;
        return entries.Any(info => {
            var isExplicitDirectory = info.Kind == SevenZipEntryKind.Path &&
                                      string.Equals(info.Name.Normalized, directoryName, StringComparison.Ordinal);
            var containsChild = info.Name.Normalized.StartsWith(directoryPrefix, StringComparison.Ordinal);
            return isExplicitDirectory || containsChild;
        });
    }

    private static async Task<SevenZipEntryInfo> FindNestedMember(
        SevenZipArchiveIdentity identity,
        IReadOnlyList<string> nesting,
        string member,
        CancellationToken token) {
        try {
            var archivePath = SevenZipArchivePath.Get(identity, new SevenZipArchiveNest(nesting));
            try {
                var entries = await archivePath.EntryInfo(token);
                var memberName = new SevenZipEntryName(member).Normalized;
                return entries.Items.FirstOrDefault(info => {
                    var isFile = info.Kind == SevenZipEntryKind.File;
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
        SevenZipArchivePath archivePath,
        string directory) {
        return new ArchiveProviderID(
            ArchiveProviderIDFormat.SevenZip,
            archivePath.FileName,
            archivePath.Nest,
            directory);
    }

    private static string FormatLocation(SevenZipArchivePath archivePath, string directory) {
        return ArchiveProviderIDCodec.Format(Location(archivePath, directory));
    }

    private SevenZipID(SevenZipArchivePath archivePath, SevenZipEntryName relativePath) {
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        ArchivePath = archivePath ?? throw new ArgumentNullException(nameof(archivePath));
    }

    public SevenZipArchiveFileEvent FileChanged => ArchivePath.FileChanged;
    public SevenZipArchiveFileEvent FileDeleted => ArchivePath.FileDeleted;

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
            var parentDirectory = new SevenZipEntryName(lastMember).Parent;
            var parentLocation = new ArchiveProviderID(
                ArchiveProviderIDFormat.SevenZip,
                ArchivePath.FileName,
                nesting,
                parentDirectory);
            field = ArchiveProviderIDCodec.Format(parentLocation);
            return field;
        }
    }

    public string FullName => field ??= FormatLocation(ArchivePath, RelativePath.Normalized);

    public SevenZipArchivePath ArchivePath { get; }
    public SevenZipEntryName RelativePath { get; }

    public void Release() {
        ArchivePath.Release();
    }

    public async Task<IReadOnlyList<SevenZipEntryInfo>> EntryInfo(CancellationToken cancellationToken) {
        var set = await ArchivePath.EntryInfo(cancellationToken);
        return set.Items
            .Where(info => info.Name.Parent == RelativePath.Normalized)
            .ToList();
    }

    public static async Task<SevenZipID> Parse(string id, SevenZipProviderFactory factory, CancellationToken token) {
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
            var providerIDIsSevenZip = providerIDIsValid && location.Format == ArchiveProviderIDFormat.SevenZip;
            if (providerIDIsSevenZip is false) {
                return null;
            }
        }
        else {
            location = new ArchiveProviderID(ArchiveProviderIDFormat.SevenZip, id, Array.Empty<string>(), "");
        }

        var hasSevenZipExtension = Path.GetExtension(location.FileName).Equals(
            ".7z",
            StringComparison.OrdinalIgnoreCase);
        if (hasSevenZipExtension is false) {
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

        var identity = await SevenZipArchiveIdentity.Create(
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

        var archivePath = SevenZipArchivePath.Get(identity, new SevenZipArchiveNest(nesting));
        var relativePath = new SevenZipEntryName(location.Directory);
        var keepArchivePath = false;
        try {
            if (relativePath.Parts.Count > 0) {
                var entries = await archivePath.EntryInfo(token);
                if (IsDirectory(relativePath, entries.Items) is false) {
                    return null;
                }
            }

            var icon = default(object);
            var iconService = factory.IconSevenZipFileInfo;
            if (iconService is not null) {
                icon = await iconService.Icon(file, token);
            }
            var parsedID = new SevenZipID(archivePath, relativePath) {
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
