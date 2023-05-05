using Brows.Exports;
using Domore.Conf.Cli;
using System.IO;
using System.Linq;

namespace Brows.Commands {
    internal sealed class Rename : FileSystemCommand<Rename.Parameter> {
        private bool Prompt(Context context) {
            if (context == null) return false;
            if (context.HasCommander(out var commander) == false) return false;
            if (context.HasPanel(out var active) == false) return false;
            if (active.HasSelection(out var entries) == false) return false;
            var pattern = "";
            var extension = "";
            if (entries.Count == 1) {
                pattern = entries.First().Name;
                extension = Path.GetExtension(pattern) ?? "";
            }
            else {
                pattern = "<%><.>";
                extension = "";
                var exts = entries
                    .Select(entry => Path.GetExtension(entry.Name))
                    .Where(ext => !string.IsNullOrWhiteSpace(ext))
                    .Distinct()
                    .ToList();
                if (exts.Count == 1) {
                    pattern = $"<%>{exts[0]}";
                    extension = exts[0];
                }
            }
            var trigger = InputTrigger;
            var input = $"{trigger} \"{pattern}\"";
            var inputStart = trigger.Length + 2;
            var inputLength = pattern.Length - extension.Length;
            return context.Operate(async (progress, token) => {
                return await commander.ShowPalette(input, inputStart, inputLength, token);
            });
        }

        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasGesture(out _)) {
                return Prompt(context);
            }
            if (false == context.HasParameter(out var parameter)) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == active.HasFileSystemRename(out var rename)) return false;
            if (false == active.HasFileSystemDirectory(out var directory)) {
                return false;
            }
            var service = Service;
            if (service == null) {
                return false;
            }
            var pattern = parameter.Pattern?.Trim() ?? "";
            if (pattern == "") {
                return false;
            }
            var dict = rename.Selection(pattern);
            if (dict == null || dict.Count == 0) {
                return false;
            }
            return context.Operate(async (progress, token) => {
                return await service.Work(directory.FullName, dict.ToDictionary(pair => pair.Key.Name, pair => pair.Value), progress, token);
            });
        }

        public IRenameDirectoryEntries Service { get; set; }

        public sealed class Parameter {
            [CliArgument]
            public string Pattern { get; set; }
        }
    }
}
