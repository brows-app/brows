using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    using Threading.Tasks;

    public static class FileSystem {
        public static async Task<FileInfo> FileExists(string path, CancellationToken cancellationToken) {
            return await Async.With(cancellationToken).Run(() => {
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

        public static async Task<DirectoryInfo> DirectoryExists(string path, CancellationToken cancellationToken) {
            return await Async.With(cancellationToken).Run(() => {
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

        public static async Task<FileSystemInfo> InfoExists(string path, CancellationToken cancellationToken) {
            var fileTask = FileExists(path, cancellationToken);
            var directoryTask = DirectoryExists(path, cancellationToken);
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
    }
}
