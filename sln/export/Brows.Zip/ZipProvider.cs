using Domore.IO;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Exports;

    internal sealed class ZipProvider : Provider<ZipEntry, ZipConfig> {
        private static readonly ILog Log = Logging.For(typeof(ZipProvider));

        private async Task ZipFileDeleted() {
            var directory = Zip.File.Directory;
            for (; ; ) {
                if (directory == null) {
                    await Change(Drives.ID, Token);
                    return;
                }
                var existing = await FileSystemTask.ExistingDirectory(directory.FullName, Token);
                if (existing != null) {
                    await Change(existing.FullName, Token);
                    return;
                }
                directory = directory.Parent;
            }
        }

        private async Task ZipFileChanged() {
            await Refresh(Token);
        }

        private async Task<IReadOnlyList<ZipEntry>> List(CancellationToken cancellationToken) {
            var entryInfo = await Zip.EntryInfo(cancellationToken);
            return entryInfo
                .Select(info => new ZipEntry(this, info))
                .ToList();
        }

        protected sealed override async Task Begin(CancellationToken cancellationToken) {
            await Provide(await List(cancellationToken));
            Zip.FileChanged.Add(ZipFileChanged);
            Zip.FileDeleted.Add(ZipFileDeleted);
        }

        protected sealed override void End() {
            Zip.FileChanged.Remove(ZipFileChanged);
            Zip.FileDeleted.Remove(ZipFileDeleted);
            Zip.Release();
        }

        protected sealed override async Task Refresh(CancellationToken cancellationToken) {
            var entries = await List(cancellationToken);
            await Revoke(Provided);
            await Provide(entries);
        }

        protected sealed override async Task<bool> Drop(IPanelDrop data, IOperationProgress progress, CancellationToken cancellationToken) {
            return await Zip.ArchivePath.Drop(data, progress, cancellationToken);
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
            public async Task<bool> Work(ICollection<IProvidedIO> io, IProvider target, IOperationProgress progress, CancellationToken token) {
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

        private sealed class CopyFromProvidedIO : ICopyProvidedIO, IProviderExport<ZipProvider> {
            public async Task<bool> Work(IEnumerable<IProvidedIO> io, IProvider target, IOperationProgress progress, CancellationToken token) {
                if (io is null) throw new ArgumentNullException(nameof(io));
                var files = io.Files();
                if (files.Count > 0) {
                    return await new CopyFromFiles().Work(files, target, progress, token);
                }
                var streams = io.StreamSets();
                if (streams.Count > 0) {
                    return await new CopyFromStreams().Work(streams, target, progress, token);
                }
                return false;
            }
        }

        private sealed class CopyFromFiles {
            public async Task<bool> Work(IEnumerable<string> files, IProvider target, IOperationProgress progress, CancellationToken token) {
                if (target is not ZipProvider provider) {
                    return false;
                }
                var list = files?.Where(file => !string.IsNullOrWhiteSpace(file))?.ToList();
                if (list == null) return false;
                if (list.Count == 0) {
                    return false;
                }
                if (progress != null) {
                    progress.Change(addTarget: list.Count);
                }
                var zipFiles = await Task.Run(cancellationToken: token, function: () => {
                    var fileIsDir = list.Count == 1;
                    var fileInfos = list.SelectMany(item => {
                        if (token.IsCancellationRequested) {
                            token.ThrowIfCancellationRequested();
                        }
                        if (progress != null) {
                            progress.Change(data: Path.GetFileName(item));
                            progress.Change(1);
                        }
                        var infos = Array.Empty<FileInfo>().AsEnumerable();
                        var f = new FileInfo(item);
                        if (f.Exists) {
                            infos = new[] { f };
                            fileIsDir = false;
                        }
                        else {
                            var d = new DirectoryInfo(item);
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
                    var zipFiles = fileNames
                        .Select(f => new {
                            Path = f.OriginalPath,
                            Name = f.RelativePath
                                .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                .Replace(Path.DirectorySeparatorChar, '/')
                                .Replace(Path.AltDirectorySeparatorChar, '/')
                        })
                        .ToList();
                    return zipFiles;
                });
                await provider.Zip.ArchivePath.Update(progress: progress, token: token, info: new() {
                    CreateEntriesFromFiles = zipFiles.ToDictionary(
                        file => string.IsNullOrWhiteSpace(provider.Zip.RelativePath.Original)
                            ? file.Name
                            : string.Join("/", provider.Zip.RelativePath.Original, file.Name),
                        file => file.Path)
                });
                return true;
            }
        }

        private sealed class CopyFromStreams {
            public async Task<bool> Work(IEnumerable<IEntryStreamSet> entryStreamSets, IProvider target, IOperationProgress progress, CancellationToken token) {
                var sets = entryStreamSets.Where(s => s is not null)?.ToList();
                if (sets == null) return false;
                if (sets.Count == 0) return false;
                if (target is not ZipProvider provider) {
                    return false;
                }
                var archive = provider.Zip.ArchivePath;
                await archive.Update(progress: progress, token: token, info: new ZipArchiveUpdate {
                    CopyStreamSets = sets
                });
                return true;
            }
        }
    }
}
