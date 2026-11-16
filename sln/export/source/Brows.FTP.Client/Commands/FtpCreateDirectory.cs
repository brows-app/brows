using Domore.Conf.Cli;

namespace Brows.Commands;

internal abstract class FtpCreateDirectory<TProvider> : FtpCommand<TProvider, FtpCreateDirectory<TProvider>.Parameter>
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
        var directoryUri = FileProtocolUri.Extend(provider.Uri, [name], includeTrailingSlash: true);
        var relativePath = FileProtocolUri.RelativePath(provider.Uri, directoryUri);
        var path = FileProtocolUri.CombinePath(provider.Uri, relativePath);
        return context.Operate(async (progress, token) => {
            await provider.ClientAt(provider.Uri).CreateDirectory(path, token);
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
