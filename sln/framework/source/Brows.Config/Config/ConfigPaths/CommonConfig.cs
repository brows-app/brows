using Brows.Composition;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config.ConfigPaths;

/// <summary>
/// The exported implementation of <see cref="IConfigCommon"/>.
/// </summary>
internal sealed class CommonConfig : ConfigPath, IConfigCommon, IExportAndVary<ConfigExportVariable> {
    private string DefaultRootFolder => field ??=
        Environment.GetFolderPath(
            Environment.SpecialFolder.CommonApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);

    protected internal sealed override string Root => _Root ??=
        Path.Combine(DefaultRootFolder, AppName, "Config");
    private string _Root;

    protected internal sealed override ConfigKind Kind =>
        ConfigKind.Common;

    Task IExportAndVary<ConfigExportVariable>.Vary(ConfigExportVariable variable, CancellationToken token) {
        if (token.IsCancellationRequested) {
            return Task.FromCanceled(token);
        }
        _Root = ChangeRoot(
            path: variable?.CommonConfig?.Path,
            defaultPathRoot: DefaultRootFolder);
        return Task.CompletedTask;
    }
}
