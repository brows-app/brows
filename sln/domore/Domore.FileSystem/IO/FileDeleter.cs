using Domore.Logs;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.IO {
    internal sealed class FileDeleter {
        private static readonly ILog Log = Logging.For(typeof(FileDeleter));

        public FileInfo File { get; }

        public FileDeleter(FileInfo file) {
            File = file ?? throw new ArgumentNullException(nameof(file));
        }

        public Task Delete(CancellationToken cancellationToken) {
            return Delete(File, cancellationToken);
        }

        public static Task Delete(FileInfo file, CancellationToken cancellationToken) {
            ArgumentNullException.ThrowIfNull(file);
            if (Log.Debug()) {
                Log.Debug(nameof(Delete) + " > " + file.FullName);
            }
            return Task.Run(cancellationToken: cancellationToken, action: () => {
                if (file == null) return;
                if (file.Exists == false) return;
                try {
                    file.Delete();
                }
                catch (UnauthorizedAccessException ex) {
                    if (Log.Info()) {
                        Log.Info(nameof(UnauthorizedAccessException) + " > " + file.FullName, ex);
                    }
                    file.Attributes = FileAttributes.Normal;
                    file.Refresh();
                    file.Delete();
                }
            });
        }
    }
}
