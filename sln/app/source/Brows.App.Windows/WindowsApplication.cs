using Brows.Windows;
using System;
using System.Windows;

namespace Brows;

internal sealed class WindowsApplication : Application {
    private void Exports_Ready(object sender, EventArgs e) {
        Theme.LoadExportResources(Exports.Components.ApplicationResourceDictionaries);
    }

    internal AppTheme Theme { get; }

    public AppExports Exports { get; }
    public AppService Service { get; }
    public CancellationToken Token { get; }

    public WindowsApplication(CancellationToken token) {
        _ = new AppLogger(this);
        Token = token;
        Exports = new AppExports(this);
        Theme = new AppTheme(Resources);
        Exports.Ready += Exports_Ready;
        Service = new AppService(this);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }
}
