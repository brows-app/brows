using Domore.Conf;
using Domore.Logs;
using Domore.Runtime.Win32;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal class WindowsProgramInstance {
        private static readonly ILog Log = Logging.For(typeof(WindowsProgramInstance));

        private readonly TaskCompletionSource<int> TaskSource = new TaskCompletionSource<int>();
        private readonly Thread Thread;

        private void ThreadStart() {
            var consoleAlloced = false;
            try {
                CancellationToken.ThrowIfCancellationRequested();
                //var
                //splashScreen = new SplashScreen("splash.png");
                //splashScreen.Show(true);
                CancellationToken.ThrowIfCancellationRequested();

                var app = new WindowsApplication { Context = Context };
                CancellationToken.ThrowIfCancellationRequested();

                var config = Conf.Configure(new Windows());
                var console = config.Console;
                if (console) {
                    consoleAlloced = kernel32.AttachConsole(-1)
                        ? false
                        : kernel32.AllocConsole();
                }
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
            finally {
                if (consoleAlloced) {
                    kernel32.FreeConsole();
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

        private class Windows {
            public bool Console { get; set; }
        }
    }
}
