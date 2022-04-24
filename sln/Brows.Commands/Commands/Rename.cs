using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class Rename : Command<Rename.Info>, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.F2);
                yield return new InputTrigger("rename");
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(Context context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasPanel(out var active)) {
                if (context.HasKey(out _)) {
                    if (context.HasCommander(out var commander)) {
                        var rename = "";
                        var renameExt = "";
                        var entries = active.Selection().ToList();
                        if (entries.Count == 1) {
                            rename = entries[0].Name;
                            renameExt = Path.GetExtension(rename) ?? "";
                        }
                        else {
                            rename = "%";
                            renameExt = "";
                            var exts = entries
                                .Select(entry => Path.GetExtension(entry.Name))
                                .Where(ext => !string.IsNullOrWhiteSpace(ext))
                                .Distinct()
                                .ToList();
                            if (exts.Count == 1) {
                                rename = $"%{exts[0]}";
                                renameExt = exts[0];
                            }
                        }
                        var trigger = InputTrigger();
                        var input = $"{trigger} {rename}";
                        var inputStart = trigger.Length + 1;
                        var inputLength = rename.Length - renameExt.Length;
                        await commander.ShowPalette(input, inputStart, inputLength, cancellationToken);
                        return true;
                    }
                }
                if (context.HasParameter(out var parameter)) {
                    var entries = active.Selection().ToList();
                    if (entries.Count == 1) {
                        var
                        entry = entries[0];
                        entry.Rename(parameter.Name);
                    }
                    else {
                        var rename = parameter.Name?.Trim() ?? "";
                        if (rename.Contains('%') || rename.Contains('*')) {
                            var orderedEntries = entries.OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                            for (var i = 0; i < orderedEntries.Count; i++) {
                                var entry = orderedEntries[i];
                                var entryRename = rename
                                    .Replace("*", Path.GetFileNameWithoutExtension(entry.Name))
                                    .Replace("%", $"{i + 1}");
                                entry.Rename(entryRename);
                            }
                        }
                        else {
                            return false;
                        }
                    }
                    await active.Deploy(renameEntries: entries, cancellationToken: cancellationToken);
                    return true;
                }
            }
            return false;
        }

        public class Info {
            [Argument(Name = "name", Order = 0, Required = true)]
            public string Name { get; set; }
        }
    }
}
