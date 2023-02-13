using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class Renamer {
        private static readonly Dictionary<string, Func<IEntry, int, string>> Replacement = new Dictionary<string, Func<IEntry, int, string>> {
            { "*", (e, i) => Path.GetFileNameWithoutExtension(e.Name) },
            { "%", (e, i) => i.ToString() }
        };

        public static async Task<IReadOnlyList<IEntry>> Rename(ICommandContext context, string pattern, CancellationToken cancellationToken) {
            var list = new List<IEntry>();
            if (context.HasPanel(out var active)) {
                var selection = active.Selection().OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                var entries = active.Entries.Items.Except(selection).ToList();
                var count = selection.Count;
                if (count > 0) {
                    if (context.HasProvider(out var provider)) {
                        var cs = await provider.CaseSensitive(cancellationToken);
                        var cmp = cs ? StringComparer.CurrentCulture : StringComparer.CurrentCultureIgnoreCase;
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
                            entry.Rename(rename);
                            list.Add(entry);
                        }
                    }
                }
            }
            return list;
        }
    }
}
