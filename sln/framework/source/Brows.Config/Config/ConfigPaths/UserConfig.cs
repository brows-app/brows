using Brows.Composition;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config.ConfigPaths;

/// <summary>
/// The exported implementation of <see cref="IConfigUser"/>.
/// </summary>
internal sealed class UserConfig : ConfigPath, IConfigUser, IExportAndVary<ConfigExportVariable> {
    private string DefaultRootFolder => field ??=
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);

    protected internal sealed override string Root => _Root ??=
        Path.Combine(DefaultRootFolder, AppName, "Config");
    private string _Root;

    protected internal sealed override ConfigKind Kind =>
        ConfigKind.User;

    Task IExportAndVary<ConfigExportVariable>.Vary(ConfigExportVariable variable, CancellationToken token) {
        if (token.IsCancellationRequested) {
            return Task.FromCanceled(token);
        }
        _Root = ChangeRoot(
            path: variable?.UserConfig?.Path,
            defaultPathRoot: DefaultRootFolder);
        return Task.CompletedTask;
    }
}
