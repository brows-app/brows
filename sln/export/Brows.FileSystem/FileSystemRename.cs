using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Brows {
    public sealed class FileSystemRename {
        private static readonly Dictionary<string, Func<FileSystemInfo, int, string>> Replacement = new Dictionary<string, Func<FileSystemInfo, int, string>> {
            { "<*>", (e, i) => Path.GetFileNameWithoutExtension(e.Name) },
            { "<.>", (e, i) => Path.GetExtension(e.Name) },
            { "<%>", (e, i) => i.ToString() }
        };

        internal FileSystemProvider Provider { get; }

        internal FileSystemRename(FileSystemProvider provider) {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public IReadOnlyDictionary<FileSystemInfo, string> Selection(string pattern) {
            var dict = new Dictionary<FileSystemInfo, string>();
            var list = Provider.Selected.ToList();
            var count = list.Count;
            if (count > 0) {
                list.Sort(new EntryComparer(Provider.Sorting));
                var cs = Provider.CaseSensitive;
                var cmp = cs == false ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
                var uns = Provider.Provided.Except(list);
                var set = new HashSet<string>(uns.Select(e => e.Name), cmp);
                for (var i = 0; i < count; i++) {
                    var entry = list[i];
                    var info = entry.Info;
                    var rename = Replacement.Aggregate(pattern ?? "<*><.>", (name, replacement) => name.Replace(replacement.Key, replacement.Value(info, i + 1)));
                    var added = set.Add(rename);
                    if (added == false) {
                        var n = "";
                        for (var j = 1; j < int.MaxValue; j++) {
                            n = info is FileInfo
                                ? $"{Path.GetFileNameWithoutExtension(rename)} ({j}){Path.GetExtension(rename)}"
                                : $"{rename} ({j})";
                            if (set.Add(n)) {
                                break;
                            }
                        }
                        rename = n;
                    }
                    dict[info] = rename;
                }
            }
            return dict;
        }
    }
}
