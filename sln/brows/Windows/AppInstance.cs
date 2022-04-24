using System;
using System.Threading;
using System.Windows;

namespace Brows.Windows {
    internal class AppInstance {
        private readonly string MutexName = "9520EEBF-6D53-432C-9697-729DE30A31A9";
        private readonly Mutex Mutex;
        private readonly bool ShutDown;
        private readonly CommanderService Service = new CommanderService();

        private void Service_Exited(object sender, CommanderExitedEventArgs e) {
            var app = Application;
            var windows = app.Windows;
            if (windows != null) {
                foreach (var window in windows) {
                    if (window is Window w) {
                        w.Close();
                    }
                }
            }
            app.Shutdown();
        }

        private void Service_Loaded(object sender, CommanderLoadedEventArgs e) {
            if (e != null) {
                if (e.First) {
                    new AppTheme(Application, Service.ComponentResources);
                }
                var
                window = new CommanderWindow { DataContext = e.Commander };
                window.Show();
            }
        }

        private void Service_Themed(object sender, CommanderThemedEventArgs e) {
            new AppTheme(Application, Service.ComponentResources, e?.Theme);
        }

        private void Service_Logger(object sender, CommanderLoggerEventArgs e) {
            if (e != null) {
                var
                window = new GraphicalLogServiceWindow();
                window.Show();
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e) {
            Mutex?.Dispose();
        }

        private void Application_Startup(object sender, StartupEventArgs e) {
            if (ShutDown) {
                Service.Post(Environment.CommandLine, default).ContinueWith(_ => {
                    Environment.Exit(0);
                });
            }
            else {
                Service.Exited += Service_Exited;
                Service.Loaded += Service_Loaded;
                Service.Logger += Service_Logger;
                Service.Themed += Service_Themed;
                Service.Begin();
            }
        }

        public Application Application { get; }
        public bool ShuttingDown => ShutDown;

        public AppInstance(Application application) {
            Application = application ?? throw new ArgumentNullException(nameof(application));
            Application.Startup += Application_Startup;
            Application.Exit += Application_Exit;

            Mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            ShutDown = !createdNew;
        }
    }
}
