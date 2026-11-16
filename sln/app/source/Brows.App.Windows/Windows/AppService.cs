using Domore.Logs;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Brows.Windows; 
internal sealed class AppService {
    private static readonly ILog Log = Logging.For(typeof(AppService));
    private Task EndTask;

    private async void Service_Ended(object sender, CommanderEndedEventArgs e) {
        try {
            await App.Dispatcher.InvokeAsync(() => EndTask ??= EndCoreAsync()).Task.Unwrap();
        }
        catch (Exception exception) {
            Log.Error(nameof(Service_Ended), exception);
        }
    }

    private async Task EndCoreAsync() {
        await Task.Yield();
        try {
            var windows = App.Windows.Cast<Window>().ToArray();
            foreach (var window in windows) {
                var lifetime = (window as CommanderWindow)?.Commander as IAsyncDisposable;
                if (lifetime is not null) {
                    await lifetime.DisposeAsync();
                }
            }
            foreach (var window in windows) {
                if (App.Windows.Cast<Window>().Contains(window) == false) {
                    continue;
                }
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                EventHandler onClosed = (sender, args) => closed.TrySetResult();
                window.Closed += onClosed;
                try {
                    window.Close();
                    await closed.Task;
                }
                finally {
                    window.Closed -= onClosed;
                }
            }
            App.Shutdown();
        }
        catch {
            EndTask = null;
            throw;
        }
    }
    private void Service_Loaded(object sender, CommanderLoadedEventArgs e) {
        if (e != null) {
            async void show() {
                if (EndTask is not null) {
                    try {
                        if (e.Commander is IAsyncDisposable lifetime) {
                            await lifetime.DisposeAsync();
                        }
                    }
                    catch (Exception exception) {
                        Log.Error(nameof(Service_Loaded), exception);
                    }
                    return;
                }
                var window = new CommanderWindow { Commander = e.Commander };
                window.Show();
                WindowShown?.Invoke(this, e);
            }
            if (App.Dispatcher.Thread != Thread.CurrentThread) {
                App.Dispatcher.BeginInvoke(show);
            }
            else {
                show();
            }
        }
    }

    private void Exports_Ready(object sender, EventArgs e) {
        var
        domain = new CommanderDomain(App.Exports.Import);
        domain.Ended += Service_Ended;
        domain.Loaded += Service_Loaded;
        domain.Begin();
    }

    public event EventHandler WindowShown;

    public WindowsApplication App { get; }

    public AppService(WindowsApplication app) {
        App = app ?? throw new ArgumentNullException(nameof(app));
        App.Exports.Ready += Exports_Ready;
    }
}
