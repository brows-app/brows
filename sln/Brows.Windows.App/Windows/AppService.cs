using System;
using System.Threading;
using System.Windows;

namespace Brows.Windows {
    internal sealed class AppService {
        private void Service_Ended(object sender, CommanderEndedEventArgs e) {
            void close() {
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
            if (App.Dispatcher.Thread != Thread.CurrentThread) {
                App.Dispatcher.BeginInvoke(close);
            }
            else {
                close();
            }
        }

        private void Service_Loaded(object sender, CommanderLoadedEventArgs e) {
            if (e != null) {
                void show() {
                    var
                    window = new CommanderWindow { DataContext = e.Commander };
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
}
