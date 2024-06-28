using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Domore.IO {
    public static class FileSystemPath {
        public static string CommonOf(IEnumerable<string> paths, StringComparer comparer) {
            ArgumentNullException.ThrowIfNull(paths);
            ArgumentNullException.ThrowIfNull(comparer);
            var common = default(List<string>);
            var items = paths
                .Where(path => path is not null)
                .Distinct(comparer)
                .Select(path => new {
                    Path = path,
                    Parts = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                })
                .OrderBy(item => item.Parts.Length)
                .ToList();
            foreach (var item in items) {
                if (common == null) {
                    common = item.Parts.ToList();
                }
                else {
                    for (var i = 0; i < item.Parts.Length; i++) {
                        if (common.Count > i) {
                            if (comparer.Equals(common[i], item.Parts[i])) {
                                continue;
                            }
                            common.RemoveRange(i, common.Count - i);
                        }
                        break;
                    }
                }
            }
            return string.Join(Path.DirectorySeparatorChar, common ?? new List<string>());
        }

        public static IEnumerable<(string OriginalPath, string RelativePath)> SkipCommonOf(IEnumerable<string> paths, StringComparer comparer, int backtrack = 0) {
            if (null == paths) throw new ArgumentNullException(nameof(paths));
            var list = paths.Where(path => path is not null).Distinct(comparer).ToList();
            if (list.Count == 1) {
                return new[] { (list[0], Path.GetFileName(list[0])) };
            }
            var common = CommonOf(list, comparer);
            var commonParts = common.Split(Path.DirectorySeparatorChar);
            return list
                .Select(path => {
                    var originalPath = path;
                    var relativePath = string.Join(Path.DirectorySeparatorChar, path
                        .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                        .Skip(commonParts.Length - backtrack));
                    return (originalPath, relativePath);
                });
        }
    }
}
