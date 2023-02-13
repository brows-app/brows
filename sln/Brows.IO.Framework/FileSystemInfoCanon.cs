using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Threading.Tasks;

    public class FileSystemInfoCanon {
        private EnumerationOptions EnumerationOptions =>
            _EnumerationOptions ?? (
            _EnumerationOptions = new EnumerationOptions {
                AttributesToSkip = 0,
                IgnoreInaccessible = false,
                MatchCasing = MatchCasing.CaseInsensitive,
                RecurseSubdirectories = false
            });
        private EnumerationOptions _EnumerationOptions;

        private async Task<string> FindCanonicalFullName(FileSystemInfo fileSystemInfo, CancellationToken cancellationToken) {
            if (null == fileSystemInfo) throw new ArgumentNullException(nameof(fileSystemInfo));

            var dir = fileSystemInfo as DirectoryInfo ?? new DirectoryInfo(fileSystemInfo.FullName);
            var parts = new List<string>();
            var parent = dir.Parent;
            while (parent != null) {
                var opts = EnumerationOptions;
                var part = await Async.With(cancellationToken).Run(() => {
                    foreach (var info in parent.EnumerateFileSystemInfos(dir.Name, opts)) {
                        return info;
                    }
                    return null;
                });
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

        public async Task<string> GetCanonicalFullName(FileSystemInfo fileSystemInfo, CancellationToken cancellationToken) {
            return await FindCanonicalFullName(fileSystemInfo, cancellationToken);
        }
    }
}
