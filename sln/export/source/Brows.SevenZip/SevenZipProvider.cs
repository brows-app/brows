using Brows.Commands;
using Brows.Composition;
using Brows.Entries;
using Brows.Exports;
using Brows.IO.Helpers;
using Brows.Panels;
using Brows.Providers;
using Domore.IO;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class SevenZipProvider : Provider<SevenZipEntry, SevenZipConfig> {
    private static readonly ILog Log = Logging.For(typeof(SevenZipProvider));

    private async Task SevenZipFileDeleted(CancellationToken token) {
        var directory = Archive.File.Directory;
        for (; ; ) {
            if (directory is null) {
                await Change(Drives.ID, token);
                return;
            }
            var existing = await FileSystemTask.ExistingDirectory(directory.FullName, token);
            if (existing is not null) {
                await Change(existing.FullName, token);
                return;
            }
            directory = directory.Parent;
        }
    }

    private Task SevenZipFileChanged(CancellationToken token) {
        return Refresh(token);
    }

    private async Task<IReadOnlyList<SevenZipEntry>> List(CancellationToken token) {
        var entryInfo = await Archive.EntryInfo(token);
        return entryInfo
            .Select(info => new SevenZipEntry(this, info))
            .ToList();
    }

    protected sealed override async Task Begin(CancellationToken token) {
        await Provide(await List(token), token);
        Archive.FileChanged.Add(SevenZipFileChanged);
        Archive.FileDeleted.Add(SevenZipFileDeleted);
    }

    protected sealed override void End() {
        Archive.FileChanged.Remove(SevenZipFileChanged);
        Archive.FileDeleted.Remove(SevenZipFileDeleted);
        Archive.Release();
    }

    protected sealed override async Task Refresh(CancellationToken token) {
        var entries = await List(token);
        await Revoke(Provided, token);
        await Provide(entries, token);
    }

    protected sealed override Task<bool> Drop(IPanelDrop data, IOperationProgress progress, CancellationToken token) {
        return Archive.ArchivePath.Drop(data, progress, token);
    }

    public SevenZipID Archive { get; }

    public object Icon =>
        Archive.Icon;

    public new SevenZipConfig Config =>
        base.Config;

    public sealed override string Parent =>
        Archive.Parent;

    public SevenZipProviderFactory Factory { get; }

    public SevenZipProvider(SevenZipID archive, SevenZipProviderFactory factory) : base(archive?.FullName) {
        Archive = archive ?? throw new ArgumentNullException(nameof(archive));
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    private sealed class ProvideIO : IProvideIO, IProviderExport<SevenZipProvider> {
        public async Task<bool> Work(
            ICollection<IProvidedIO> io,
            ICommandSource source,
            IProvider target,
            IOperationProgress progress,
            CancellationToken token) {
            if (io is null) {
                throw new ArgumentNullException(nameof(io));
            }
            if (target is not SevenZipProvider provider) {
                return false;
            }
            var selection = provider.Selected;
            var selectionIsEmpty = selection.Any() is false;
            if (selectionIsEmpty) {
                return false;
            }
            io.Add(new ProvidedIO {
                StreamSets = new[] { new SevenZipStreamSet(selection
                    .SelectMany(z => z.Descendants.Prepend(z))
                    .DistinctBy(z => z.ID)
                    .Select(z => z.Stream?.Source)
                    .Where(s => s is not null)) }
            });
            await Task.CompletedTask;
            return true;
        }
    }

    private sealed class CopyProvidedIO : ICopyProvidedIO, IProviderExport<SevenZipProvider> {
        private static async Task<IReadOnlyList<string>> FindEmptyDirectories(
            IReadOnlyList<string> fileSources,
            CancellationToken token) {
            return await Task.Run(cancellationToken: token, function: () => {
                IEnumerable<string> emptyDirectories(DirectoryInfo directory) {
                    token.ThrowIfCancellationRequested();
                    var descendants = directory.EnumerateFileSystemInfos().ToList();
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

        public async Task<bool> Work(
            IEnumerable<IProvidedIO> io,
            IProvider target,
            IOperationProgress progress,
            CancellationToken token) {
            if (io is null) {
                throw new ArgumentNullException(nameof(io));
            }
            if (target is not SevenZipProvider provider) {
                return false;
            }
            var provided = io.ToList();
            var fileSources = provided.FileSources();
            var files = await provided.FlattenFiles(progress, token);
            var emptyDirectories = await FindEmptyDirectories(fileSources, token);
            if (emptyDirectories.Count > 0) {
                var paths = files.Select(file => file.OriginalPath).Concat(emptyDirectories);
                var sourceIsOneDirectory = fileSources.Count == 1 && File.Exists(fileSources[0]) is false;
                var sourceDirectory = sourceIsOneDirectory
                    ? new DirectoryInfo(fileSources[0]).FullName
                    : null;
                var emptyDirectoryIsNested = emptyDirectories.Any(path =>
                    StringComparer.OrdinalIgnoreCase.Equals(
                        new DirectoryInfo(path).FullName,
                        sourceDirectory) is false);
                var sourceDirectoryHasContents = files.Count > 0 || emptyDirectoryIsNested;
                var backtrack = sourceIsOneDirectory && sourceDirectoryHasContents ? 1 : 0;
                var names = FileSystemPath.SkipCommonOf(
                    paths,
                    StringComparer.Ordinal,
                    backtrack: backtrack);
                files = names
                    .Select(file => new ProvidedFile(
                        file.OriginalPath,
                        file.RelativePath
                            .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            .Replace(Path.DirectorySeparatorChar, '/')
                            .Replace(Path.AltDirectorySeparatorChar, '/')))
                    .ToList();
            }
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
                SevenZipProvider provider,
                IOperationProgress progress,
                CancellationToken token) {
                if (files is null) {
                    throw new ArgumentNullException(nameof(files));
                }
                if (provider is null) {
                    throw new ArgumentNullException(nameof(provider));
                }
                await provider.Archive.ArchivePath.Update(progress: progress, token: token, info: new() {
                    CreateEntriesFromFiles = files.ToDictionary(
                        file => string.IsNullOrWhiteSpace(provider.Archive.RelativePath.Original)
                            ? file.RelativePath
                            : string.Join(
                                "/",
                                provider.Archive.RelativePath.Original.Replace('\\', '/'),
                                file.RelativePath),
                        file => file.OriginalPath)
                });
                return true;
            }
        }

        private sealed class CopyFromStreams {
            public async Task<bool> Work(
                IEnumerable<IEntryStreamSet> entryStreamSets,
                SevenZipProvider provider,
                IOperationProgress progress,
                CancellationToken token) {
                if (provider is null) {
                    throw new ArgumentNullException(nameof(provider));
                }
                if (entryStreamSets is null) {
                    throw new ArgumentNullException(nameof(entryStreamSets));
                }
                var sets = entryStreamSets.Where(s => s is not null).ToList();
                if (sets.Count == 0) {
                    return false;
                }
                var archive = provider.Archive.ArchivePath;
                await archive.Update(progress: progress, token: token, info: new SevenZipArchiveUpdate {
                    CopyStreamSets = sets,
                    CopyStreamTargetDirectory = provider.Archive.RelativePath.Original
                });
                return true;
            }
        }
    }
}
