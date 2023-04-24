using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Brows.Commands {
    internal static class Renamer {
        private static readonly Dictionary<string, Func<FileSystemInfo, int, string>> Replacement = new Dictionary<string, Func<FileSystemInfo, int, string>> {
            { "<*>", (e, i) => Path.GetFileNameWithoutExtension(e.Name) },
            { "<.>", (e, i) => Path.GetExtension(e.Name) },
            { "<%>", (e, i) => i.ToString() }
        };

        public static IReadOnlyDictionary<string, string> Rename(ICommandContext context, string pattern) {
            if (context is null) return null;
            if (context.HasPanel(out var active) == false) return null;
            if (active.HasFileSystemSelection(out var activeSelection) == false) return null;
            if (active.HasFileSystemInfo(out var activeEntries) == false) return null;
            if (active.HasFileSystemCaseSensitivity(out var caseSensitive) == false) return null;
            var dict = new Dictionary<string, string>();
            var selection = activeSelection.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var count = selection.Count;
            if (count > 0) {
                var entries = activeEntries.Except(selection).ToList();
                var cs = caseSensitive;
                var cmp = cs == false ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
                var set = new HashSet<string>(entries.Select(e => e.Name), cmp);
                for (var i = 0; i < count; i++) {
                    var entry = selection[i];
                    var rename = Replacement.Aggregate(pattern, (name, replacement) => name.Replace(replacement.Key, replacement.Value(entry, i + 1)));
                    var added = set.Add(rename);
                    if (added == false) {
                        var n = "";
                        for (var j = 1; j < int.MaxValue; j++) {
                            n = $"{rename} ({j})";
                            if (set.Add(n)) {
                                break;
                            }
                        }
                        rename = n;
                    }
                    dict[entry.Name] = rename;
                }
            }
            return dict;
        }
    }
}
