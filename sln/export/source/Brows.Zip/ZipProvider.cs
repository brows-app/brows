using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows;

internal sealed class ZipProvider : Provider<ZipEntry, ZipConfig> {
    private static readonly ILog Log = Logging.For(typeof(ZipProvider));

    private async Task ZipFileDeleted(CancellationToken token) {
        var directory = Zip.File.Directory;
        for (; ; ) {
            if (directory == null) {
                await Change(Drives.ID, token);
                return;
            }
            var existing = await FileSystemTask.ExistingDirectory(directory.FullName, token);
            if (existing != null) {
                await Change(existing.FullName, token);
                return;
            }
            directory = directory.Parent;
        }
    }

    private Task ZipFileChanged(CancellationToken token) {
        return Refresh(token);
    }

    private async Task<IReadOnlyList<ZipEntry>> List(CancellationToken token) {
        var entryInfo = await Zip.EntryInfo(token);
        return entryInfo
            .Select(info => new ZipEntry(this, info))
            .ToList();
    }

    protected sealed override async Task Begin(CancellationToken token) {
        await Provide(await List(token), token);
        Zip.FileChanged.Add(ZipFileChanged);
        Zip.FileDeleted.Add(ZipFileDeleted);
    }

    protected sealed override void End() {
        Zip.FileChanged.Remove(ZipFileChanged);
        Zip.FileDeleted.Remove(ZipFileDeleted);
        Zip.Release();
    }

    protected sealed override async Task Refresh(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        Zip.ArchivePath.InvalidateEntryInfo();
        var entries = await List(token);
        await Revoke(Provided, token);
        await Provide(entries, token);
    }

    protected sealed override Task<bool> Drop(IPanelDrop data, IOperationProgress progress, CancellationToken token) {
        return Zip.ArchivePath.Drop(data, Zip.RelativePath.Original, progress, token);
    }

    public ZipID Zip { get; }

    public object Icon =>
        Zip.Icon;

    public new ZipConfig Config =>
        base.Config;

    public sealed override string Parent =>
        Zip.Parent;

    public ZipProviderFactory Factory { get; }

    public ZipProvider(ZipID zip, ZipProviderFactory factory) : base(zip?.FullName) {
        Zip = zip ?? throw new ArgumentNullException(nameof(zip));
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    private sealed class ProvideIO : IProvideIO, IProviderExport<ZipProvider> {
        public async Task<bool> Work(
            ICollection<IProvidedIO> io,
            ICommandSource source,
            IProvider target,
            IOperationProgress progress,
            CancellationToken token) {
            if (io is null) throw new ArgumentNullException(nameof(io));
            if (target is not ZipProvider provider) {
                return false;
            }
            var selection = provider.Selected;
            if (selection.Any() == false) {
                return false;
            }
            io.Add(new ProvidedIO {
                StreamSets = new[] { new ZipStreamSet(selection
                    .SelectMany(z => z.Descendants.Prepend(z))
                    .DistinctBy(z => z.ID)
                    .Select(z => z.Stream?.Source)
                    .Where(s => s != null))
                }
            });
            await Task.CompletedTask;
            return true;
        }
    }

    private sealed class CopyProvidedIO : ICopyProvidedIO, IProviderExport<ZipProvider> {
        private static async Task<IReadOnlyList<string>> FindEmptyDirectories(
            IReadOnlyList<string> fileSources,
            CancellationToken token) {
            return await Task.Run(cancellationToken: token, function: () => {
                IEnumerable<string> emptyDirectories(DirectoryInfo directory) {
                    token.ThrowIfCancellationRequested();
                    var descendants = new List<FileSystemInfo>();
                    foreach (var descendant in directory.EnumerateFileSystemInfos()) {
                        token.ThrowIfCancellationRequested();
                        descendants.Add(descendant);
                    }
                    if (descendants.Count == 0) {
                        yield return directory.FullName;
                        yield break;
                    }
                    foreach (var descendant in descendants.OfType<DirectoryInfo>()) {
                        foreach (var empty in emptyDirectories(descendant)) {
                            yield return empty;
                        }
                    }
                }

                var empty = new List<string>();
                foreach (var source in fileSources) {
                    token.ThrowIfCancellationRequested();
                    var directory = new DirectoryInfo(source);
                    if (directory.Exists is false) {
                        continue;
                    }
                    empty.AddRange(emptyDirectories(directory));
                }
                return empty;
            });
        }

        private static string GetPathRoot(string originalPath, string relativePath) {
            var relativeParts = relativePath.Split(
                new[] { '/', '\\' },
                StringSplitOptions.RemoveEmptyEntries);
            var root = Path.GetFullPath(originalPath);
            for (var index = 0; index < relativeParts.Length; index++) {
                var parent = Path.GetDirectoryName(root);
                root = parent ?? Path.GetPathRoot(root) ?? root;
            }
            return root;
        }

        private static string GetFileRoot(ProvidedFile file) {
            return GetPathRoot(file.OriginalPath, file.RelativePath);
        }

        private static async Task<IReadOnlyList<ProvidedFile>> GetEmptyDirectoryFiles(
            IReadOnlyList<string> fileSources,
            IReadOnlyList<ProvidedFile> files,
            IReadOnlyList<string> emptyDirectories,
            CancellationToken token) {
            if (emptyDirectories.Count == 0) {
                return Array.Empty<ProvidedFile>();
            }
            return await Task.Run(cancellationToken: token, function: () => {
                var sourceIsOneDirectory = fileSources.Count == 1 &&
                    File.Exists(fileSources[0]) is false &&
                    Directory.Exists(fileSources[0]);
                var filesOrOneSourceDirectoryExist = files.Count > 0 || sourceIsOneDirectory;
                if (filesOrOneSourceDirectoryExist) {
                    var sourceDirectory = sourceIsOneDirectory
                        ? new DirectoryInfo(fileSources[0]).FullName
                        : null;
                    var relativeRoot = files.Count > 0
                        ? GetFileRoot(files[0])
                        : Path.GetDirectoryName(sourceDirectory)
                            ?? Path.GetPathRoot(sourceDirectory)
                            ?? sourceDirectory;
                    return emptyDirectories.Select(path => {
                        token.ThrowIfCancellationRequested();
                        var fullPath = Path.GetFullPath(path);
                        var relativePath = Path.GetRelativePath(relativeRoot, fullPath);
                        var isSelectedDirectory = sourceDirectory is not null &&
                            StringComparer.OrdinalIgnoreCase.Equals(fullPath, sourceDirectory);
                        var relativePathEscapesRoot = relativePath == ".." ||
                            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                            Path.IsPathRooted(relativePath);
                        if (isSelectedDirectory || relativePathEscapesRoot) {
                            relativePath = Path.GetFileName(fullPath);
                        }
                        return new ProvidedFile(path, NormalizePath(relativePath));
                    }).ToList();
                }
                var firstSource = FileSystemPath.SkipCommonOf(fileSources, StringComparer.Ordinal).First();
                var selectionRoot = GetPathRoot(firstSource.OriginalPath, firstSource.RelativePath);
                return emptyDirectories.Select(path => {
                    token.ThrowIfCancellationRequested();
                    var fullPath = Path.GetFullPath(path);
                    var relativePath = Path.GetRelativePath(selectionRoot, fullPath);
                    var relativePathEscapesRoot = relativePath == ".." ||
                        relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                        Path.IsPathRooted(relativePath);
                    if (relativePathEscapesRoot) {
                        relativePath = Path.GetFileName(fullPath);
                    }
                    return new ProvidedFile(path, NormalizePath(relativePath));
                }).ToList();
            });
        }

        private static string NormalizePath(string path) {
            return path
                .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
        }

        private static string TargetName(string directory, string relativePath, bool isDirectory) {
            var path = string.IsNullOrEmpty(directory)
                ? relativePath
                : string.Join("/", directory.Replace('\\', '/'), relativePath);
            return isDirectory && path.EndsWith('/') == false
                ? path + "/"
                : path;
        }

        public async Task<bool> Work(
            IEnumerable<IProvidedIO> io,
            IProvider target,
            IOperationProgress progress,
            CancellationToken token) {
            if (io is null) throw new ArgumentNullException(nameof(io));
            if (target is not ZipProvider provider) {
                return false;
            }
            var provided = io.ToList();
            var fileSources = provided.FileSources();
            var files = await provided.FlattenFiles(progress, token);
            var emptyDirectories = await FindEmptyDirectories(fileSources, token);
            var emptyDirectoryFiles = await GetEmptyDirectoryFiles(fileSources, files, emptyDirectories, token);
            files = files.Concat(emptyDirectoryFiles).ToList();
            if (files.Count > 0) {
                return await new CopyFromFiles().Work(files, provider, progress, token);
            }
            var streams = provided.StreamSets();
            if (streams.Count > 0) {
                return await new CopyFromStreams().Work(streams, provider, progress, token);
            }
            return false;
        }

        private sealed class CopyFromFiles {
            public async Task<bool> Work(
                IEnumerable<ProvidedFile> files,
                ZipProvider provider,
                IOperationProgress progress,
                CancellationToken token) {
                if (null == files) throw new ArgumentNullException(nameof(files));
                if (null == provider) throw new ArgumentNullException(nameof(provider));
                await provider.Zip.ArchivePath.Update(progress: progress, token: token, info: new() {
                    CreateEntriesFromFiles = files.ToDictionary(
                        file => TargetName(
                            provider.Zip.RelativePath.Normalized,
                            file.RelativePath,
                            Directory.Exists(file.OriginalPath)),
                        file => Directory.Exists(file.OriginalPath) ? null : file.OriginalPath)
                });
                return true;
            }
        }

        private sealed class CopyFromStreams {
            public async Task<bool> Work(
                IEnumerable<IEntryStreamSet> entryStreamSets,
                ZipProvider provider,
                IOperationProgress progress,
                CancellationToken token) {
                if (null == provider) throw new ArgumentNullException(nameof(provider));
                if (null == entryStreamSets) throw new ArgumentNullException(nameof(entryStreamSets));
                var sets = entryStreamSets.Where(s => s is not null)?.ToList();
                if (sets == null) return false;
                if (sets.Count == 0) return false;
                var archive = provider.Zip.ArchivePath;
                await archive.Update(progress: progress, token: token, info: new ZipArchiveUpdate {
                    CopyStreamSets = sets,
                    CopyStreamTargetDirectory = provider.Zip.RelativePath.Original
                });
                return true;
            }
        }
    }
}
