﻿using Brows.Exports;
using Domore.IO;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
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
            var entries = await List(token);
            await Revoke(Provided, token);
            await Provide(entries, token);
        }

        protected sealed override Task<bool> Drop(IPanelDrop data, IOperationProgress progress, CancellationToken token) {
            return Zip.ArchivePath.Drop(data, progress, token);
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
            public async Task<bool> Work(ICollection<IProvidedIO> io, ICommandSource source, IProvider target, IOperationProgress progress, CancellationToken token) {
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
            public async Task<bool> Work(IEnumerable<IProvidedIO> io, IProvider target, IOperationProgress progress, CancellationToken token) {
                if (io is null) throw new ArgumentNullException(nameof(io));
                if (target is not ZipProvider provider) {
                    return false;
                }
                var files = await io.FlattenFiles(progress, token);
                if (files.Count > 0) {
                    return await new CopyFromFiles().Work(files, provider, progress, token);
                }
                var streams = io.StreamSets();
                if (streams.Count > 0) {
                    return await new CopyFromStreams().Work(streams, provider, progress, token);
                }
                return false;
            }

            private sealed class CopyFromFiles {
                public async Task<bool> Work(IEnumerable<ProvidedFile> files, ZipProvider provider, IOperationProgress progress, CancellationToken token) {
                    if (null == files) throw new ArgumentNullException(nameof(files));
                    if (null == provider) throw new ArgumentNullException(nameof(provider));
                    await provider.Zip.ArchivePath.Update(progress: progress, token: token, info: new() {
                        CreateEntriesFromFiles = files.ToDictionary(
                            file => string.IsNullOrWhiteSpace(provider.Zip.RelativePath.Original)
                                ? file.RelativePath
                                : string.Join("/", provider.Zip.RelativePath.Original, file.RelativePath),
                            file => file.OriginalPath)
                    });
                    return true;
                }
            }

            private sealed class CopyFromStreams {
                public async Task<bool> Work(IEnumerable<IEntryStreamSet> entryStreamSets, ZipProvider provider, IOperationProgress progress, CancellationToken token) {
                    if (null == provider) throw new ArgumentNullException(nameof(provider));
                    if (null == entryStreamSets) throw new ArgumentNullException(nameof(entryStreamSets));
                    var sets = entryStreamSets.Where(s => s is not null)?.ToList();
                    if (sets == null) return false;
                    if (sets.Count == 0) return false;
                    var archive = provider.Zip.ArchivePath;
                    await archive.Update(progress: progress, token: token, info: new ZipArchiveUpdate {
                        CopyStreamSets = sets
                    });
                    return true;
                }
            }
        }
    }
}
