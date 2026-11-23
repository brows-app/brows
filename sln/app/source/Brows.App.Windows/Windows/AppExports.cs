using Brows.Composition;
using Domore.Threading.Tasks;
using System;
using System.Threading;
using System.Windows;
using TASK = System.Threading.Tasks.Task;

namespace Brows.Windows;

internal sealed class AppExports {
    private TaskCache<AppExports> Task => field ??= new(token => TASK.Run(cancellationToken: token, function: () => {
        Import = Imports.Current;
        Components = AppComponentCollection.From(Import);
        return this;
    }));

    private async void App_Startup(object sender, StartupEventArgs e) {
        SynchronizationContext = SynchronizationContext.Current;
        await Task.Ready(App.Token);
        Ready?.Invoke(this, e);
    }

    public event EventHandler Ready;

    public IImport Import { get; private set; }
    public AppComponentCollection Components { get; private set; }
    public SynchronizationContext SynchronizationContext { get; private set; }
    public WindowsApplication App { get; }

    public AppExports(WindowsApplication app) {
        App = app ?? throw new ArgumentNullException(nameof(app));
        App.Startup += App_Startup;
    }
}
