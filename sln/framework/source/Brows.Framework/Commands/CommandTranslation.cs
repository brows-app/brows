using Brows.Localization;
using System;
using System.Linq;
using System.Text;

namespace Brows.Commands;

internal static class CommandTranslation {
    public static string Group(ITranslation translation, string name) {
        return string.IsNullOrWhiteSpace(name)
            ? translation?.Value($"Command_Group_{nameof(Command)}")
            : translation?.Value($"Command_Group_{name}");
    }

    public static string Description(ITranslation translation, string name) {
        return translation?.Value($"Command_Description_{name}");
    }

    public static string Help(ITranslation translation, string name) {
        return translation?.Value($"Command_Help_{name}");
    }

    public static string Conf(Command command) {
        if (null == command) throw new ArgumentNullException(nameof(command));
        var translation = command.Translate;
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
        builder.AppendLine(comment(Description(translation, command.Name)));
        builder.AppendLine();
        builder.AppendLine(translation
            ?.Value($"Command_Conf_{command.Name}")
            ?.Replace("{input}", command.Trigger?.Inputs?.Main?.Input));
        return builder.ToString().Trim();
    }
}
