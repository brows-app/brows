using Domore.Conf.Cli;
using System.Threading.Tasks;

namespace Brows.Commands;

internal abstract class FtpRename<TProvider> : FtpCommand<TProvider, FtpRename<TProvider>.Parameter>
where TProvider : FtpProviderBase {
    private bool Prompt(Context context, FtpEntry entry) {
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
        if (!context.HasPanel(out var panel)) {
            return false;
        }
        if (!panel.HasProvider<TProvider>(out var provider)) {
            return false;
        }
        if (!context.HasSource(out FtpEntry _, out var entries)) {
            return false;
        }
        if (entries.Count != 1) {
            return false;
        }

        if (context.HasGesture(out _)) {
            return Prompt(context, entries[0]);
        }
        if (!context.HasParameter(out var parameter)) {
            if (context.HasParameterError(out _)) {
                return false;
            }
            return Prompt(context, entries[0]);
        }

        var name = parameter.Name;
        var nameIsInvalid = string.IsNullOrEmpty(name) || name.Contains('/');
        if (nameIsInvalid) {
            return false;
        }
        var entry = entries[0];
        if (string.Equals(name, entry.Name, StringComparison.Ordinal)) {
            return true;
        }
        return context.Operate(async (progress, token) => {
            token.ThrowIfCancellationRequested();
            var parentUri = FileProtocolUri.Parent(entry.Uri) ?? provider.Uri;
            var destinationUri = FileProtocolUri.Extend(
                parentUri,
                [name],
                includeTrailingSlash: entry.Kind == FileProtocolEntryKind.Directory);
            var destinationRelativePath = FileProtocolUri.RelativePath(provider.Uri, destinationUri);
            var destinationPath = FileProtocolUri.CombinePath(provider.Uri, destinationRelativePath);
            await Rename(provider, entry, destinationPath, token);
            await Refresh(provider, token);
            return true;
        });
    }

    protected virtual Task Rename(TProvider provider,
                                  FtpEntry entry,
                                  string destinationPath,
                                  CancellationToken token) {
        return provider.ClientFor(entry).Rename(entry.RemotePath, destinationPath, token);
    }

    protected virtual Task Refresh(TProvider provider, CancellationToken token) {
        return ((IProvider)provider).Refresh(token);
    }

    public sealed class Parameter {
        [CliArgument]
        [CliRequired]
        public string Name { get; set; }
    }
}
