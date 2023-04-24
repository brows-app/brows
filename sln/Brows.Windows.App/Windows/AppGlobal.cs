using System;
using System.Windows;

namespace Brows.Windows {
    internal sealed class AppGlobal {
        private void App_Startup(object sender, StartupEventArgs e) {
            var
            translation = new AppComponentTranslation(App.Exports.Components);
            translation.Load();
            Translation.Global = translation;
        }

        public WindowsApplication App { get; }

        public AppGlobal(WindowsApplication app) {
            App = app ?? throw new ArgumentNullException(nameof(app));
            App.Startup += App_Startup;
        }
    }
}
