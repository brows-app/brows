using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Brows;

internal sealed class WindowsProgramInstance {
    private readonly TaskCompletionSource<int> TaskSource = new();
    private readonly Thread Thread;

    private void ThreadStart() {
        var task = TaskSource;
        var token = Token;
        try {
            var nativeSplash = default(SplashScreen);
            var customSplash = default(SplashWindow);
            var showSplash = Config?.Splash?.Show == true;
            if (showSplash) {
                nativeSplash = new SplashScreen(GetType().Assembly, "splash.png");
                nativeSplash.Show(autoClose: false, topMost: false);
                token.ThrowIfCancellationRequested();
                customSplash = new SplashWindow();
                customSplash.Show();
                nativeSplash.Close(TimeSpan.Zero);
                token.ThrowIfCancellationRequested();
            }
            var appWindowShown = default(EventHandler);
            var
            app = new WindowsApplication(Token);
            app.Service.WindowShown += appWindowShown = (s, e) => {
                app.Service.WindowShown -= appWindowShown;
                if (customSplash != null) {
                    customSplash.Close();
                }
            };
            token.ThrowIfCancellationRequested();
            task.SetResult(app.Run());
        }
        catch (Exception ex) {
            if (ex is OperationCanceledException && Token.IsCancellationRequested) {
                task.SetCanceled(Token);
            }
            else {
                task.SetException(ex);
            }
        }
    }

    public Task<int> Task =>
        TaskSource.Task;

    public CancellationToken Token { get; }
    public WindowsProgramConfig Config { get; }

    public WindowsProgramInstance(WindowsProgramConfig config, CancellationToken token) {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Token = token;
        Thread = new Thread(ThreadStart);
        Thread.IsBackground = true;
        Thread.Name = typeof(WindowsProgramInstance).FullName;
        Thread.SetApartmentState(ApartmentState.STA);
        Thread.Start();
    }
}
