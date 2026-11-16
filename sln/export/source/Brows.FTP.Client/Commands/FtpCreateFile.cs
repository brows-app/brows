using Domore.Conf.Cli;

namespace Brows.Commands;

internal abstract class FtpCreateFile<TProvider> : FtpCommand<TProvider, FtpCreateFile<TProvider>.Parameter>
where TProvider : FtpProviderBase {
    protected sealed override bool Work(Context context) {
        if (context is null || !context.GetParameter(out var parameter) ||
            !context.HasPanel(out var panel) || !panel.HasProvider<TProvider>(out var provider)) {
            return false;
        }
        var name = parameter.Name;
        if (string.IsNullOrEmpty(name) || name.Contains('/')) {
            return false;
        }
        var fileUri = FileProtocolUri.Extend(provider.Uri, [name], includeTrailingSlash: false);
        var relativePath = FileProtocolUri.RelativePath(provider.Uri, fileUri);
        var path = FileProtocolUri.CombinePath(provider.Uri, relativePath);
        return context.Operate(async (progress, token) => {
            await provider.ClientAt(provider.Uri).CreateFile(path, name, token);
            await ((IProvider)provider).Refresh(token);
            return true;
        });
    }

    public sealed class Parameter {
        [CliArgument]
        [CliRequired]
        public string Name { get; set; }
    }
}
