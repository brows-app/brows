using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Brows;

public sealed class FileSystemRename {
    private static readonly Dictionary<string, Func<FileSystemInfo, int, string>> Replacement = new Dictionary<string, Func<FileSystemInfo, int, string>> {
        { "<*>", (e, i) => Path.GetFileNameWithoutExtension(e.Name) },
        { "<.>", (e, i) => Path.GetExtension(e.Name) },
        { "<%>", (e, i) => i.ToString() }
    };

    private static string Expand(string pattern, FileSystemInfo info, int index) {
        return Replacement.Aggregate(pattern ?? "<*><.>", (name, replacement) => {
            return name.Replace(replacement.Key, replacement.Value(info, index));
        });
    }

    private List<FileSystemEntry> SortedSelection() {
        var list = Provider.Selected.ToList();
        list.Sort(new EntryComparer(Provider.Sorting));
        return list;
    }

    internal FileSystemProvider Provider { get; }

    internal StringComparer NameComparer =>
        Provider.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    internal FileSystemRename(FileSystemProvider provider) {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /*
     * Expands the pattern for each selected entry, in sorted order, without
     * changing names that collide with each other or with existing entries.
     */
    internal IReadOnlyList<KeyValuePair<FileSystemInfo, string>> Expand(string pattern) {
        var list = SortedSelection();
        var expansion = new List<KeyValuePair<FileSystemInfo, string>>(list.Count);
        for (var i = 0; i < list.Count; i++) {
            var info = list[i].Info;
            expansion.Add(new(info, Expand(pattern, info, i + 1)));
        }
        return expansion;
    }

    public IReadOnlyDictionary<FileSystemInfo, string> Selection(string pattern) {
        var dict = new Dictionary<FileSystemInfo, string>();
        var list = SortedSelection();
        var count = list.Count;
        if (count > 0) {
            var cmp = NameComparer;
            var uns = Provider.Provided.Except(list);
            var set = new HashSet<string>(uns.Select(e => e.Name), cmp);
            for (var i = 0; i < count; i++) {
                var entry = list[i];
                var info = entry.Info;
                var rename = Expand(pattern, info, i + 1);
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
