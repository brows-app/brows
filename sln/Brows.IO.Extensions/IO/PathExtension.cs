using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Brows.IO {
    public static class PathExtension {
        public static string CommonOf(IEnumerable<string> paths, StringComparer comparer) {
            if (null == paths) throw new ArgumentNullException(nameof(paths));
            if (null == comparer) throw new ArgumentNullException(nameof(comparer));
            var common = default(List<string>);
            var items = paths
                .Distinct(comparer)
                .Select(path => new {
                    Path = path,
                    Parts = path?.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries) ?? new string[] { }
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

        public static IEnumerable<(string OriginalPath, string RelativePath)> SkipCommonOf(IEnumerable<string> paths, StringComparer comparer) {
            if (null == paths) throw new ArgumentNullException(nameof(paths));
            var common = CommonOf(paths, comparer);
            var commonParts = common.Split(Path.DirectorySeparatorChar);
            return paths
                .Select(path => (path, string.Join(
                    Path.DirectorySeparatorChar,
                    path is null
                        ? null
                        : path
                            .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                            .Skip(commonParts.Length))));
        }
    }
}
