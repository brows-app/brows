using Domore.IO;
using System;
using System.Threading;
using System.Windows;

namespace Brows.Windows {
    internal sealed class AppConfig {
        private void App_Startup(object sender, StartupEventArgs e) {
            Request.Factory = new RequestFactory();
            CommanderTheme.Service = new AppTheme.Service(App);
            FileSystemEvent.SynchronizationContext = SynchronizationContext.Current;
        }

        public WindowsApplication App { get; }

        public AppConfig(WindowsApplication app) {
            App = app ?? throw new ArgumentNullException(nameof(app));
            App.Startup += App_Startup;
        }
    }
}
