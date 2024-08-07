﻿using Brows.Data;
using Brows.Exports;
using Brows.FileSystem;
using Domore.Logs;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class FileSystemProviderFactory : ProviderFactory<FileSystemProvider>, IFileSystemNavigationService {
        private static readonly ILog Log = Logging.For(typeof(FileSystemProviderFactory));

        private static async Task<DirectoryInfo> LoadDirectory(string id, CancellationToken token) {
            return await Task
                .Run(cancellationToken: token, function: () => {
                    try {
                        var info = new DirectoryInfo(id);
                        return info.Exists
                            ? info
                            : null;
                    }
                    catch {
                        return null;
                    }
                })
                .ConfigureAwait(false);
        }

        protected sealed override async Task<FileSystemProvider> CreateFor(string id, IPanel panel, CancellationToken token) {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(CreateFor), id));
            }
            var info = await LoadDirectory(id, token).ConfigureAwait(false);
            if (info == null) {
                return null;
            }
            if (Metadata == null) {
                Metadata = await FileSystemMetaDataState
                    .Load(MetadataSystemReader, token)
                    .ConfigureAwait(false);
            }
            var caseCorrect = info.FullName;
            var caseSensitive = true;
            await Task
                .WhenAll(
                    CaseCorrectFile?.Work(info.FullName, result => caseCorrect = result, token) ?? Task.CompletedTask,
                    CaseSensitiveDirectory?.Work(info.FullName, result => caseSensitive = result, token) ?? Task.CompletedTask)
                .ConfigureAwait(false);
            if (Log.Info()) {
                Log.Info(
                    Log.Join(nameof(caseCorrect), caseCorrect),
                    Log.Join(nameof(caseSensitive), caseSensitive));
            }
            return new FileSystemProvider(
                factory: this,
                caseSensitive: caseSensitive,
                initialCapacity: 25,
                directory: info.FullName == caseCorrect ? info : new DirectoryInfo(caseCorrect));
        }

        public FileSystemMetaDataState Metadata { get; private set; }

        public ICaseSensitiveDirectory CaseSensitiveDirectory { get; set; }
        public ICopyFilesToDirectory CopyFilesToDirectory { get; set; }
        public IMoveFilesToDirectory MoveFilesToDirectory { get; set; }
        public ICaseCorrectFile CaseCorrectFile { get; set; }
        public IDragSourceFileSystemInfos DragSourceFileSystemInfos { get; set; }
        public IDropDirectoryInfoData DropDirectoryInfoData { get; set; }
        public IMetadataFileReader MetadataFileReader { get; set; }
        public IMetadataSystemReader MetadataSystemReader { get; set; }
        public IFileSystemIcon FileSystemIcon { get; set; }
        public IDriveIcon DriveIcon { get; set; }
    }
}
