using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DIRECTORY = System.IO.Directory;

namespace Domore.IO {
    internal sealed class DirectoryDeleter {
        private static readonly ILog Log = Logging.For(typeof(DirectoryDeleter));

        private static Task Delete(DirectoryInfo directory, CancellationToken cancellationToken) {
            if (null == directory) throw new ArgumentNullException(nameof(directory));
            if (Log.Debug()) {
                Log.Debug(nameof(Delete) + " > " + directory.FullName);
            }
            return Task.Run(cancellationToken: cancellationToken, action: () => {
                if (directory == null) return;
                if (DIRECTORY.Exists(directory.FullName) == false) return;
                try {
                    directory.Delete(recursive: true);
                }
                catch (UnauthorizedAccessException ex) {
                    if (Log.Info()) {
                        Log.Info(nameof(UnauthorizedAccessException) + " > " + directory.FullName, ex);
                    }
                    directory.Attributes = FileAttributes.Normal;
                    directory.Refresh();
                    directory.Delete(recursive: true);
                }
            });
        }

        public DirectoryInfo Directory { get; }

        public DirectoryDeleter(DirectoryInfo directory) {
            Directory = directory ?? throw new ArgumentNullException(nameof(directory));
        }

        public async Task Delete(FileSystemProgress progress, CancellationToken cancellationToken) {
            var dirList = new List<DirectoryInfo>();
            var fileTasks = new List<Task>();
            var enumerable = Directory.EnumerateFileSystemInfosAsync("*", new EnumerationOptions {
                AttributesToSkip = 0,
                RecurseSubdirectories = true,
                ReturnSpecialDirectories = false
            });
            enumerable.Ready += (s, e) => {
                progress?.AddToTarget(enumerable.FileCount + enumerable.DirectoryCount + 1);
            };
            async Task delete(FileInfo file) {
                progress?.SetCurrentInfo(file);
                await FileDeleter
                    .Delete(file, cancellationToken)
                    .ConfigureAwait(false);
                progress?.AddToProgress(1);
            }
            await foreach (var item in enumerable.WithCancellation(cancellationToken)) {
                if (cancellationToken.IsCancellationRequested) {
                    cancellationToken.ThrowIfCancellationRequested();
                }
                if (item is FileInfo file) {
                    fileTasks.Add(delete(file));
                }
                if (item is DirectoryInfo directory) {
                    dirList.Add(directory);
                }
            }
            await Task
                .WhenAll(fileTasks)
                .ConfigureAwait(false);
            progress?.SetCurrentInfo(Directory);
            var err = default(Exception);
            try {
                await Delete(Directory, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) {
                if (Log.Info()) {
                    Log.Info(ex);
                }
                err = ex;
            }
            if (err == null) {
                progress?.AddToProgress(dirList.Count + 1);
                return;
            }
            for (; ; ) {
                if (dirList.Count == 0) {
                    break;
                }
                var dir = await Task
                    .Run(cancellationToken: cancellationToken, function: () => dirList
                        .OrderByDescending(d => d.FullName.Length)
                        .FirstOrDefault(d => DIRECTORY.Exists(d.FullName) && d.GetDirectories().Length == 0))
                    .ConfigureAwait(false);
                if (dir == null) {
                    break;
                }
                progress?.SetCurrentInfo(dir);
                await Delete(dir, cancellationToken).ConfigureAwait(false);
                progress?.AddToProgress(1);
                dirList.Remove(dir);
            }
            progress?.SetCurrentInfo(Directory);
            await Delete(Directory, cancellationToken).ConfigureAwait(false);
            progress?.AddToProgress(1);
        }
    }
}
