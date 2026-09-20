using Brows.Composition;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config.ConfigPaths;

internal sealed class DefaultConfig : ConfigPath, IConfigDefault, IExportAndVary<ConfigExportVariable> {
    protected internal sealed override string Root => _Root;
    private string _Root;

    protected internal sealed override ConfigKind Kind => _Kind ?? throw new InvalidOperationException("The config kind has not been set.");
    private ConfigKind? _Kind;

    async Task IExportAndVary<ConfigExportVariable>.Vary(ConfigExportVariable variable, CancellationToken token) {
        var kind = _Kind = variable?.DefaultConfig ?? ConfigKind.User;
        var agent = kind switch {
            ConfigKind.Common => new CommonConfig(),
            ConfigKind.User => new UserConfig() as ConfigPath,
            _ => throw new ArgumentException(paramName: nameof(variable), message: $"The config kind '{kind}' is invalid.")
        };
        var iVary = agent as IExportAndVary<ConfigExportVariable>;
        if (iVary is not null) {
            await iVary.Vary(variable, token);
        }
        _Root = agent.Root;
    }
}
