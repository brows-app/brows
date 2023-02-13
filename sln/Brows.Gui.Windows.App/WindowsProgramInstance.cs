using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal class WindowsProgramInstance {
        private readonly TaskCompletionSource<int> TaskSource = new TaskCompletionSource<int>();
        private readonly Thread Thread;

        private void ThreadStart() {
            try {
                CancellationToken.ThrowIfCancellationRequested();
                //var
                //splashScreen = new SplashScreen("splash.png");
                //splashScreen.Show(true);
                CancellationToken.ThrowIfCancellationRequested();

                var app = new WindowsApplication { Context = Context };
                CancellationToken.ThrowIfCancellationRequested();

                var exitCode = app.Run();
                TaskSource.SetResult(exitCode);
            }
            catch (Exception ex) {
                if (ex is OperationCanceledException canceled && canceled.CancellationToken == CancellationToken) {
                    TaskSource.SetCanceled(CancellationToken);
                }
                else {
                    TaskSource.SetException(ex);
                }
            }
        }

        public Task<int> Task =>
            TaskSource.Task;

        public IProgramContext Context { get; }
        public CancellationToken CancellationToken { get; }

        public WindowsProgramInstance(IProgramContext context, CancellationToken cancellationToken) {
            Context = context;
            CancellationToken = cancellationToken;
            Thread = new Thread(ThreadStart) { Name = typeof(WindowsProgramInstance).FullName, IsBackground = true };
            Thread.SetApartmentState(ApartmentState.STA);
            Thread.Start();
        }
    }
}
