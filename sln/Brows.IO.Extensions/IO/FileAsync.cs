using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    using Threading.Tasks;

    public static class FileAsync {
        public static Task Delete(string path, CancellationToken cancellationToken) {
            return Async.Run(cancellationToken, () => File.Delete(path));
        }

        public static Task<bool> Exists(string path, CancellationToken cancellationToken) {
            return Async.Run(cancellationToken, () => File.Exists(path));
        }

        public static Task<FileStream> Create(string path, CancellationToken cancellationToken) {
            return Async.Run(cancellationToken, () => File.Create(path));
        }
    }
}
