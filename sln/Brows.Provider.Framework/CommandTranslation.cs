using System;
using System.Linq;
using System.Text;

namespace Brows {
    internal static class CommandTranslation {
        private static ITranslation Translate =>
            Translation.Global;

        public static string Group(string name) {
            return string.IsNullOrWhiteSpace(name)
                ? Translate.Value($"Command_Group_{nameof(Command)}")
                : Translate.Value($"Command_Group_{name}");
        }

        public static string Description(string name) {
            return Translate.Value($"Command_Description_{name}");
        }

        public static string Help(string name) {
            return Translate.Value($"Command_Help_{name}");
        }

        public static string Conf(Command command) {
            if (null == command) throw new ArgumentNullException(nameof(command));
            string comment(string s) => "# " + s;
            var builder = new StringBuilder();
            var helpLine = command.HelpLine;
            var inputTrigger = command.Trigger?.Inputs?.Main?.Input;
            if (inputTrigger != null) {
                if (helpLine != null) {
                    helpLine = comment(inputTrigger) + " " + string.Join(Environment.NewLine, helpLine
                        .Split()
                        .Select((s, i) => i == 0
                            ? s
                            : comment(new string(' ', inputTrigger.Length + 1) + s)));
                }
            }
            if (helpLine != null) {
                builder.AppendLine(helpLine);
            }
            var gestures = command.Trigger?.Gestures;
            if (gestures != null && gestures.Count > 0) {
                builder.AppendLine(comment(""));
                foreach (var gesture in gestures) {
                    builder.AppendLine(comment($"[{gesture.Display}] {gesture.Defined}"));
                }
            }
            builder.AppendLine(comment(""));
            builder.AppendLine(comment(Description(command.Name)));
            builder.AppendLine();
            builder.AppendLine(Translate.Value($"Command_Conf_{command.Name}")?.Replace("{input}", command.Trigger?.Inputs?.Main?.Input));
            return builder.ToString().Trim();
        }
    }
}
