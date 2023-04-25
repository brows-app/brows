using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Brows {
    internal sealed class WindowsProgramInstance {
        private readonly TaskCompletionSource<int> TaskSource = new TaskCompletionSource<int>();
        private readonly Thread Thread;

        private void ThreadStart() {
            try {
                Token.ThrowIfCancellationRequested();
                var
                splashScreen = new SplashScreen(GetType().Assembly, "splash.png");
                splashScreen.Show(true);
                Token.ThrowIfCancellationRequested();

                var app = new WindowsApplication(Context);
                Token.ThrowIfCancellationRequested();

                var exitCode = app.Run();
                TaskSource.SetResult(exitCode);
            }
            catch (Exception ex) {
                if (ex is OperationCanceledException canceled && canceled.CancellationToken == Token) {
                    TaskSource.SetCanceled(Token);
                }
                else {
                    TaskSource.SetException(ex);
                }
            }
        }

        public Task<int> Task =>
            TaskSource.Task;

        public IProgramContext Context { get; }
        public CancellationToken Token { get; }

        public WindowsProgramInstance(IProgramContext context, CancellationToken token) {
            Context = context;
            Token = token;
            Thread = new Thread(ThreadStart) { Name = typeof(WindowsProgramInstance).FullName, IsBackground = true };
            Thread.SetApartmentState(ApartmentState.STA);
            Thread.Start();
        }
    }
}
