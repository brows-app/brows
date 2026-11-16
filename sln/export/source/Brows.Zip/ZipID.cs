using Brows.Shared.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows;

internal sealed class ZipID {
    private readonly ArchiveProviderID Location;
    private string _FullName;
    private string _Parent;

    private ZipID(ZipArchivePath archivePath, ZipEntryName relativePath, ArchiveProviderID location) {
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        ArchivePath = archivePath ?? throw new ArgumentNullException(nameof(archivePath));
        Location = location ?? throw new ArgumentNullException(nameof(location));
    }

    public ZipArchiveFileEvent FileChanged => ArchivePath.FileChanged;
    public ZipArchiveFileEvent FileDeleted => ArchivePath.FileDeleted;

    public object Icon { get; private set; }
    public FileInfo File => ArchivePath.File;

    public string Parent {
        get {
            if (_Parent is null) {
                if (RelativePath.Parts.Count == 0) {
                    _Parent = ArchivePath.Parent;
                }
                else {
                    _Parent = ArchiveProviderIDCodec.Format(Location.WithDirectory(RelativePath.Parent));
                }
            }
            return _Parent;
        }
    }

    public string FullName => _FullName ??= ArchiveProviderIDCodec.Format(Location);

    public ZipArchivePath ArchivePath { get; }
    public ZipEntryName RelativePath { get; }

    public void Release() {
        ArchivePath.Release();
    }

    public async Task<IReadOnlyList<ZipEntryInfo>> EntryInfo(CancellationToken cancellationToken) {
        var set = await ArchivePath.EntryInfo(cancellationToken);
        return set.Items
            .Where(info => info.Name.Parent == RelativePath.Normalized)
            .ToList();
    }

    public static async Task<ZipID> Parse(string id, ZipProviderFactory factory, CancellationToken token) {
        if (factory is null) {
            throw new ArgumentNullException(nameof(factory));
        }
        if (string.IsNullOrWhiteSpace(id)) {
            return null;
        }

        ArchiveProviderID location;
        var hasArchiveProviderPrefix = ArchiveProviderIDCodec.HasArchiveProviderPrefix(id);
        if (hasArchiveProviderPrefix) {
            var isStructuredID = ArchiveProviderIDCodec.TryParse(id, out location);
            var isZipID = isStructuredID && location.Format == ArchiveProviderIDFormat.Zip;
            if (isZipID is false) {
                return null;
            }
        }
        else {
            var isZipArchive = id.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            var isAbsolutePath = Path.IsPathFullyQualified(id);
            if (isZipArchive is false || isAbsolutePath is false) {
                return null;
            }
            location = new ArchiveProviderID(
                ArchiveProviderIDFormat.Zip,
                id,
                Array.Empty<string>(),
                "");
        }

        var fileName = location.FileName;
        var fileNameIsAbsolute = Path.IsPathFullyQualified(fileName);
        var fileNameIsZip = fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        if (fileNameIsAbsolute is false || fileNameIsZip is false) {
            return null;
        }

        var file = await Task.Run(cancellationToken: token, function: () => {
            var candidate = new FileInfo(fileName);
            return candidate.Exists ? candidate : null;
        });
        if (file is null) {
            return null;
        }

        var correctCasingService = factory.CaseCorrectFile;
        if (correctCasingService is not null) {
            var correctCase = default(string);
            var corrected = await correctCasingService.Work(file.FullName, result => correctCase = result, token);
            if (corrected && file.FullName != correctCase) {
                file = new FileInfo(correctCase);
            }
        }

        var nest = new List<string>();
        foreach (var part in location.NestedMembers) {
            var currentArchive = ZipArchivePath.Get(file, new ZipArchiveNest(nest));
            try {
                var currentEntries = await currentArchive.EntryInfo(token);
                var partName = new ZipEntryName(part).Normalized;
                var matchingEntry = currentEntries.Items.FirstOrDefault(info =>
                    info.Kind == ZipEntryKind.File &&
                    StringComparer.Ordinal.Equals(info.Name.Normalized, partName));
                if (matchingEntry is null) {
                    return null;
                }
                nest.Add(matchingEntry.Name.Original);
            }
            catch (Exception exception) when (exception is not OperationCanceledException) {
                return null;
            }
            finally {
                currentArchive.Release();
            }
        }

        var archiveNest = new ZipArchiveNest(nest);
        var archivePath = ZipArchivePath.Get(file, archiveNest);
        var keepArchivePath = false;
        try {
            var relativePath = new ZipEntryName(location.Directory);
            if (relativePath.Parts.Count > 0) {
                ZipEntryInfoCollection entries;
                try {
                    entries = await archivePath.EntryInfo(token);
                }
                catch (Exception exception) when (exception is not OperationCanceledException) {
                    return null;
                }
                var pathIsDirectory = entries.Items.Any(info =>
                    info.Kind == ZipEntryKind.Path &&
                    StringComparer.Ordinal.Equals(info.Name.Normalized, relativePath.Normalized));
                if (pathIsDirectory is false) {
                    return null;
                }
            }

            var icon = default(object);
            var iconService = factory.IconZipFileInfo;
            if (iconService is not null) {
                icon = await iconService.Icon(file, token);
            }

            var canonicalLocation = new ArchiveProviderID(
                ArchiveProviderIDFormat.Zip,
                file.FullName,
                nest,
                relativePath.Normalized);
            var parsedID = new ZipID(archivePath, relativePath, canonicalLocation) {
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
