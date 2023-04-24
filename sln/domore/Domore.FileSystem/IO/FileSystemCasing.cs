using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.IO {
    public static class FileSystemCasing {
        private static EnumerationOptions EnumerationOptions =>
            _EnumerationOptions ?? (
            _EnumerationOptions = new EnumerationOptions {
                AttributesToSkip = 0,
                IgnoreInaccessible = false,
                MatchCasing = MatchCasing.CaseInsensitive,
                RecurseSubdirectories = false
            });
        private static EnumerationOptions _EnumerationOptions;

        private static string Correct(string path, CancellationToken cancellationToken) {
            var dir = new DirectoryInfo(path);
            var opts = EnumerationOptions;
            var parts = new List<string>();
            var parent = dir.Parent;
            while (parent != null) {
                cancellationToken.ThrowIfCancellationRequested();
                var part = parent.EnumerateFileSystemInfos(dir.Name, opts).FirstOrDefault();
                if (part != null) {
                    parts.Add(part.Name);
                }
                dir = parent;
                parent = dir.Parent;
            }
            var root = dir.FullName;
            if (root.Contains(':')) {
                root = root.ToUpper();
            }
            else {
                root = string.Join("\\", root.Split('\\').Select(part => part.ToUpper()));
            }
            parts.Add(root);
            parts.Reverse();
            return Path.Combine(parts.ToArray());
        }

        public static string CorrectCasing(this FileSystemInfo fileSystemInfo) {
            if (null == fileSystemInfo) throw new ArgumentNullException(nameof(fileSystemInfo));
            var path = fileSystemInfo.FullName;
            var correct = Correct(path, CancellationToken.None);
            return correct;
        }

        public static Task<string> CorrectCasingAsync(this FileSystemInfo fileSystemInfo, CancellationToken cancellationToken) {
            if (null == fileSystemInfo) throw new ArgumentNullException(nameof(fileSystemInfo));
            var path = fileSystemInfo.FullName;
            var task = Task.Run(cancellationToken: cancellationToken, function: () => {
                var correct = Correct(path, cancellationToken);
                return correct;
            });
            return task;
        }
    }
}
