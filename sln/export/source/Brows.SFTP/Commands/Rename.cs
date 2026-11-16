using Domore.Conf.Cli;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Commands;

internal sealed class Rename : SftpCommand<Rename.Parameter> {
    private bool Prompt(Context context, SftpEntry entry) {
        var inputTrigger = InputTrigger;
        if (string.IsNullOrWhiteSpace(inputTrigger)) {
            return false;
        }

        var name = entry.Name;
        if (string.IsNullOrEmpty(name)) {
            return false;
        }
        var selectedLength = FileProtocolRenameCommandLine.SelectedLength(name, entry.Kind);
        var argument = FileProtocolRenameCommandLine.QuoteArgument(name, selectedLength, out var selectedLengthInInput);
        var input = $"{inputTrigger} {argument}";
        var selectedStart = inputTrigger.Length + 2;
        return context.ShowPalette(input, selectedStart, selectedLengthInInput);
    }

    protected sealed override IEnumerable<Type> Source { get; } = [typeof(SftpEntry)];

    protected sealed override Parameter ParameterFactory(string parameter, string conf) {
        if (!string.IsNullOrWhiteSpace(conf)) {
            return base.ParameterFactory(parameter, conf);
        }
        if (FileProtocolRenameCommandLine.TryParseArgument(parameter, out var name)) {
            return new Parameter { Name = name };
        }
        return base.ParameterFactory(parameter, conf);
    }

    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (!context.HasPanel(out var active)) {
            return false;
        }
        if (!active.HasProvider(out SftpProvider provider)) {
            return false;
        }
        if (!context.HasSource(out SftpEntry _, out var entries)) {
            return false;
        }
        var selected = entries.OfType<SftpEntry>().ToList();
        if (selected.Count != 1) {
            return false;
        }

        if (context.HasGesture(out _)) {
            return Prompt(context, selected[0]);
        }
        if (!context.HasParameter(out var parameter)) {
            if (context.HasParameterError(out _)) {
                return false;
            }
            return Prompt(context, selected[0]);
        }

        var name = parameter.Name;
        var nameIsInvalid = string.IsNullOrEmpty(name) || name is "." or ".." ||
                            name.Contains('/') || name.Contains('\0');
        if (nameIsInvalid) {
            return false;
        }
        var entry = selected[0];
        if (string.Equals(name, entry.Name, StringComparison.Ordinal)) {
            return true;
        }
        return context.Operate(async (_, token) => {
            await provider.Rename(entry, name, token);
            await new ProviderPostponedRefresh(provider).Work(final: true, token);
            return true;
        });
    }

    public sealed class Parameter {
        [CliArgument]
        [CliRequired]
        public string Name { get; set; }
    }
}
