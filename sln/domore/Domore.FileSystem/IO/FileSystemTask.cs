using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.IO {
    public static class FileSystemTask {
        public static async Task<FileInfo> ExistingFile(string path, CancellationToken cancellationToken) {
            return await Task.Run(cancellationToken: cancellationToken, function: () => {
                var file = default(FileInfo);
                try {
                    file = new FileInfo(path);
                }
                catch {
                    return null;
                }
                cancellationToken.ThrowIfCancellationRequested();
                return file.Exists ? file : null;
            });
        }

        public static async Task<DirectoryInfo> ExistingDirectory(string path, CancellationToken cancellationToken) {
            return await Task.Run(cancellationToken: cancellationToken, function: () => {
                var directory = default(DirectoryInfo);
                try {
                    directory = new DirectoryInfo(path);
                }
                catch {
                    return null;
                }
                cancellationToken.ThrowIfCancellationRequested();
                return directory.Exists ? directory : null;
            });
        }

        public static async Task<FileSystemInfo> Existing(string path, CancellationToken cancellationToken) {
            var fileTask = ExistingFile(path, cancellationToken);
            var directoryTask = ExistingDirectory(path, cancellationToken);
            var first = await Task.WhenAny(fileTask, directoryTask);
            if (first is Task<FileInfo> file && file.Result != null) {
                return file.Result;
            }
            if (first is Task<DirectoryInfo> directory && directory.Result != null) {
                return directory.Result;
            }
            await Task.WhenAll(fileTask, directoryTask);
            return fileTask.Result ?? directoryTask.Result as FileSystemInfo;
        }

        public static async Task<string> Nonexistent(string path, FileSystemCollisionPrevention collision, CancellationToken cancellationToken) {
            collision = collision ?? FileSystemCollisionPrevention.Default;
            return await Task.Run(cancellationToken: cancellationToken, function: () => {
                for (; ; ) {
                    if (cancellationToken.IsCancellationRequested) {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    var fil = new FileInfo(path);
                    var dir = new DirectoryInfo(path);
                    if (dir.Exists == false && fil.Exists == false) {
                        return path;
                    }
                    path = collision.Rename(path);
                }
            });
        }

        public static async Task Delete(FileSystemInfo fileSystemInfo, FileSystemProgress fileSystemProgress, CancellationToken cancellationToken) {
            if (fileSystemInfo is DirectoryInfo directory) {
                await new DirectoryDeleter(directory).Delete(fileSystemProgress, cancellationToken);
                return;
            }
            if (fileSystemInfo is FileInfo file) {
                fileSystemProgress?.SetCurrentInfo(file);
                fileSystemProgress?.AddToTarget(1);
                await FileDeleter.Delete(file, cancellationToken);
                fileSystemProgress?.AddToProgress(1);
                return;
            }
        }

        public static async Task<DirectoryInfo> CreateDirectory(string path, CancellationToken cancellationToken) {
            return await Task.Run(cancellationToken: cancellationToken, function: () => {
                Directory.CreateDirectory(path);
                var directory = new DirectoryInfo(path);
                if (directory.Exists == false) {
                    throw new IOException();
                }
                return directory;
            });
        }
    }
}
