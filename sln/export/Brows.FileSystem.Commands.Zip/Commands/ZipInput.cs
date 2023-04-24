using Domore.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Brows.Commands {
    internal sealed class ZipInput {
        public bool CaseSensitive { get; }
        public IReadOnlyList<FileSystemInfo> FileSystemInfo { get; }

        public ZipInput(bool caseSensitive, IReadOnlyList<FileSystemInfo> fileSystemInfo) {
            CaseSensitive = caseSensitive;
            FileSystemInfo = fileSystemInfo ?? throw new ArgumentNullException(nameof(fileSystemInfo));
        }

        public IEnumerable<ZipEntry> Entries(IOperationProgress progress, CancellationToken token) {
            IEnumerable<FileInfo> files(FileSystemInfo info) {
                switch (info) {
                    case FileInfo f:
                        if (token.IsCancellationRequested) {
                            token.ThrowIfCancellationRequested();
                        }
                        if (progress != null) {
                            progress.Info.Data(f.Name);
                        }
                        yield return f;
                        break;
                    case DirectoryInfo d:
                        if (progress != null) {
                            progress.Info.Data(d.Name);
                        }
                        foreach (var file in d.EnumerateFiles("*", SearchOption.AllDirectories)) {
                            if (token.IsCancellationRequested) {
                                token.ThrowIfCancellationRequested();
                            }
                            if (progress != null) {
                                progress.Info.Data(file.Name);
                            }
                            yield return file;
                        }
                        break;
                }
            }
            var paths = FileSystemInfo.SelectMany(info => files(info)).Select(f => f.FullName);
            var common = FileSystemPath.SkipCommonOf(paths, CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
            return common
                .Select(c => new ZipEntry {
                    EntryName = c.RelativePath
                        .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Replace(Path.DirectorySeparatorChar, '/')
                        .Replace(Path.AltDirectorySeparatorChar, '/'),
                    FilePath = c.OriginalPath
                });
        }
    }
}
