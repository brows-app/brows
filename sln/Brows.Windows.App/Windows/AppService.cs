using System;
using System.Windows;

namespace Brows.Windows {
    internal sealed class AppService {
        private void Service_Ended(object sender, CommanderEndedEventArgs e) {
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
                var
                window = new CommanderWindow { DataContext = e.Commander };
                window.Show();
                WindowShown?.Invoke(this, e);
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
}
