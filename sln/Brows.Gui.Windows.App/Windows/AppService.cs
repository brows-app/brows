using System;
using System.Windows;

namespace Brows.Windows {
    internal class AppService {
        private CommanderService Service =>
            _Service ?? (
            _Service = new CommanderService(App.Context?.Composition));
        private CommanderService _Service;

        private void Service_Exited(object sender, CommanderExitedEventArgs e) {
            var app = App;
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
                    new AppTheme(App, Components);
                }
                var
                window = new CommanderWindow { DataContext = e.Commander };
                window.Show();
            }
        }

        private void Service_Themed(object sender, CommanderThemedEventArgs e) {
            new AppTheme(App, Components, e?.Theme);
        }

        private void App_Startup(object sender, StartupEventArgs e) {
            new AppGlobal(App);
            new AppConfig(App);
            new AppLogger(App);
            Service.Exited += Service_Exited;
            Service.Loaded += Service_Loaded;
            Service.Themed += Service_Themed;
            Service.Begin();
        }

        public AppComponentCollection Components =>
            _Components ?? (
            _Components = AppComponentCollection.From(Service.ComponentResources));
        private AppComponentCollection _Components;

        public WindowsApplication App { get; }

        public AppService(WindowsApplication app) {
            App = app ?? throw new ArgumentNullException(nameof(app));
            App.Startup += App_Startup;
        }
    }
}
