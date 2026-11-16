using Domore.Conf.Cli;
using System.Collections.Generic;
using System.IO;

namespace Brows.Commands;

internal sealed class CreateDirectory : SftpCommand<CreateDirectory.Parameter> {
    protected sealed override IEnumerable<Type> Source { get; } = [typeof(SftpEntry), typeof(IEntryObservation)];

    protected sealed override bool Work(Context context) {
        if (context is null || !context.HasPanel(out var active) || !active.HasProvider(out SftpProvider provider)) {
            return false;
        }
        if (!context.HasParameter(out var parameter)) {
            if (!context.HasGesture(out _)) {
                return false;
            }
            var trigger = InputTrigger;
            var placeholder = "'New directory'";
            return context.ShowPalette($"{trigger} {placeholder}", trigger.Length + 2, placeholder.Length - 2);
        }
        var name = parameter.Name?.Trim();
        var nameIsInvalid = string.IsNullOrWhiteSpace(name) ||
                            name is "." or ".." ||
                            name.Contains('/') ||
                            name.Contains('\0');
        if (nameIsInvalid) {
            return false;
        }
        var path = FileProtocolUri.CombinePath(provider.Uri, name);
        return context.Operate(async (_, token) => {
            await provider.Client.CreateDirectory(path, token);
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
