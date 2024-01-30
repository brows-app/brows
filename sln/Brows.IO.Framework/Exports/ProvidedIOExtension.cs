using Domore.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public static class ProvidedIOExtension {
        public static IReadOnlyList<string> FileSources(this IEnumerable<IProvidedIO> providedIO) {
            if (null == providedIO) throw new ArgumentNullException(nameof(providedIO));
            return providedIO
                .StreamSets()
                .SelectMany(s => s.FileSource())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct()
                .ToList();
        }

        public static IReadOnlyList<IEntryStreamSet> StreamSets(this IEnumerable<IProvidedIO> providedIO) {
            if (null == providedIO) throw new ArgumentNullException(nameof(providedIO));
            return providedIO
                .SelectMany(io => io.StreamSets ?? Array.Empty<IEntryStreamSet>())
                .Where(streamSet => streamSet is not null)
                .ToList();
        }

        public static async Task<IReadOnlyList<ProvidedFile>> FlattenFiles(this IEnumerable<IProvidedIO> providedIO, IOperationProgress progress, CancellationToken token) {
            var files = providedIO.FileSources();
            if (files.Count == 0) {
                return [];
            }
            if (progress != null) {
                progress.Change(addTarget: files.Count);
            }
            return await Task.Run(cancellationToken: token, function: () => {
                var fileIsDir = files.Count == 1;
                var fileInfos = files.SelectMany(file => {
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    if (progress != null) {
                        progress.Change(data: Path.GetFileName(file));
                        progress.Change(1);
                    }
                    var infos = Array.Empty<FileInfo>().AsEnumerable();
                    var info = new FileInfo(file);
                    if (info.Exists) {
                        infos = new[] { info };
                        fileIsDir = false;
                    }
                    else {
                        var d = new DirectoryInfo(file);
                        if (d.Exists) {
                            infos = d.EnumerateFiles(searchPattern: "*", searchOption: SearchOption.AllDirectories);
                        }
                    }
                    return infos.Select(f => {
                        if (token.IsCancellationRequested) {
                            token.ThrowIfCancellationRequested();
                        }
                        if (progress != null) {
                            progress.Change(data: f.Name);
                        }
                        return f;
                    });
                });
                var filePaths = fileInfos.Select(f => f.FullName).ToList();
                var fileNames = FileSystemPath.SkipCommonOf(filePaths, StringComparer.Ordinal, backtrack: fileIsDir ? 1 : 0).ToList();
                var flattened = fileNames
                    .Select(f => new ProvidedFile(f.OriginalPath, f.RelativePath
                        .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Replace(Path.DirectorySeparatorChar, '/')
                        .Replace(Path.AltDirectorySeparatorChar, '/')))
                    .ToList();
                return flattened;
            });
        }
    }
}
