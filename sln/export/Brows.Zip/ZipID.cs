using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal class ZipID {
        private ZipID(ZipArchivePath archivePath, ZipEntryName relativePath) {
            RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
            ArchivePath = archivePath ?? throw new ArgumentNullException(nameof(archivePath));
        }

        public ZipArchiveFileEvent FileChanged => ArchivePath.FileChanged;
        public ZipArchiveFileEvent FileDeleted => ArchivePath.FileDeleted;

        public object Icon { get; private set; }
        public FileInfo File => ArchivePath.File;

        public string Parent {
            get {
                if (_Parent == null) {
                    if (string.IsNullOrWhiteSpace(RelativePath.Original)) {
                        return _Parent = ArchivePath.Parent;
                    }
                    if (RelativePath.Parts.Count < 2) {
                        return _Parent = ArchivePath.FullName;
                    }
                    _Parent = $"{ArchivePath.FullName}>{RelativePath.Parent}";
                }
                return _Parent;
            }
        }
        private string _Parent;

        public string FullName =>
            _FullName ?? (
            _FullName = $"{ArchivePath.FullName}>{RelativePath.Normalized}");
        private string _FullName;

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
            if (null == factory) throw new ArgumentNullException(nameof(factory));
            if (string.IsNullOrWhiteSpace(id)) {
                return null;
            }
            var nest = default(List<string>);
            var path = default(List<string>);
            var file = await Task.Run(cancellationToken: token, function: () => {
                bool isZip(string s) => s?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true;
                var parts = id.Split(new[] { '>' }, StringSplitOptions.RemoveEmptyEntries);
                var filePart = parts[0];
                var fileIsZip = isZip(filePart);
                if (fileIsZip == false) {
                    return null;
                }
                var file = new FileInfo(filePart);
                if (file.Exists == false) {
                    return null;
                }
                nest = parts.Skip(1).TakeWhile(part => isZip(part)).ToList();
                path = parts.Skip(1).SkipWhile(part => isZip(part)).ToList();
                return file;
            });
            if (file == null) {
                return null;
            }
            if (path.Count > 1) {
                return null;
            }
            var correctCasingService = factory.CaseCorrectFile;
            if (correctCasingService != null) {
                var correctCase = default(string);
                var corrected = await correctCasingService.Work(file.FullName, result => correctCase = result, token);
                if (corrected) {
                    if (file.FullName != correctCase) {
                        file = new FileInfo(correctCase);
                    }
                }
            }
            var icon = default(object);
            var iconService = factory.IconZipFileInfo;
            if (iconService != null) {
                icon = await iconService.Icon(file, token);
            }
            var archiveNest = new ZipArchiveNest(nest);
            var archivePath = ZipArchivePath.Get(file, archiveNest);
            var relativePath = new ZipEntryName(path.FirstOrDefault() ?? "");
            return new ZipID(archivePath, relativePath) {
                Icon = icon
            };
        }
    }
}
