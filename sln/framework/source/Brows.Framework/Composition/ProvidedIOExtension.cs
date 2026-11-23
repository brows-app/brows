using Brows.Entries;
using Brows.IO.Helpers;
using Brows.Operations;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Provides helpers for collecting transfer sources and their file paths.
/// </summary>
public static class ProvidedIOExtension {
    /// <summary>
    /// Collects distinct, nonblank file-system paths from transfer sources.
    /// </summary>
    /// <param name="providedIO">
    /// The transfer sources to inspect.
    /// </param>
    /// <returns>
    /// The source paths.
    /// </returns>
    public static IReadOnlyList<string> FileSources(this IEnumerable<IProvidedIO> providedIO) {
        if (null == providedIO) throw new ArgumentNullException(nameof(providedIO));
        return providedIO
            .StreamSets()
            .SelectMany(s => s.FileSource())
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Collects the non-null stream sets from transfer sources.
    /// </summary>
    /// <param name="providedIO">
    /// The transfer sources to inspect.
    /// </param>
    /// <returns>
    /// The stream sets, in source order.
    /// </returns>
    public static IReadOnlyList<IEntryStreamSet> StreamSets(this IEnumerable<IProvidedIO> providedIO) {
        if (null == providedIO) throw new ArgumentNullException(nameof(providedIO));
        return providedIO
            .SelectMany(io => io.StreamSets ?? Array.Empty<IEntryStreamSet>())
            .Where(streamSet => streamSet is not null)
            .ToList();
    }

    /// <summary>
    /// Expands source files and directories into files with relative destination paths.
    /// </summary>
    /// <param name="providedIO">
    /// The transfer sources to expand.
    /// </param>
    /// <param name="progress">
    /// Receives progress updates for the operation.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The existing files, with common parent paths removed and forward slashes in relative paths.
    /// </returns>
    public static async Task<IReadOnlyList<ProvidedFile>>
    FlattenFiles(this IEnumerable<IProvidedIO> providedIO, IOperationProgress progress, CancellationToken token) {
        var files = providedIO.FileSources();
        if (files.Count == 0) {
            return [];
        }
        return await Task.Run(cancellationToken: token, function: () => {
            var fileIsDir = files.Count == 1;
            var fileInfos = files.SelectMany(file => {
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                if (progress != null) {
                    progress.Change(data: Path.GetFileName(file));
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
            var fileNames = FileSystemPath.SkipCommonOf(filePaths, StringComparer.Ordinal,
                backtrack: fileIsDir ? 1 : 0).ToList();
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
