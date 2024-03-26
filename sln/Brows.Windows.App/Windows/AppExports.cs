using Brows.Gui;
using Domore.IO;
using Domore.Threading.Tasks;
using System;
using System.Threading;
using System.Windows;
using COMMANDERTHEME = Brows.CommanderTheme;
using REQUEST = Brows.Request;
using TRANSLATION = Brows.Translation;

namespace Brows.Windows {
    internal sealed class AppExports {
        private TaskCache<AppExports> Task => _Task ??= new(async token => {
            Import = await Imports.Ready(token);
            Components = AppComponentCollection.From(Import.List<IExportResource>());
            REQUEST.Factory = RequestFactory = new RequestFactory();
            TRANSLATION.Global = Translation = await AppComponentTranslation.Ready(Components, token);
            COMMANDERTHEME.Service = CommanderTheme = new AppTheme.Service(App);
            FileSystemEvent.SynchronizationContext = SynchronizationContext;
            return this;
        });
        private TaskCache<AppExports> _Task;

        private async void App_Startup(object sender, StartupEventArgs e) {
            SynchronizationContext = SynchronizationContext.Current;
            await Task.Ready(App.Token);
            Ready?.Invoke(this, e);
        }

        public event EventHandler Ready;

        public IImport Import { get; private set; }
        public IRequestFactory RequestFactory { get; private set; }
        public ICommanderTheme CommanderTheme { get; private set; }
        public AppComponentCollection Components { get; private set; }
        public AppComponentTranslation Translation { get; private set; }
        public SynchronizationContext SynchronizationContext { get; private set; }
        public WindowsApplication App { get; }

        public AppExports(WindowsApplication app) {
            App = app ?? throw new ArgumentNullException(nameof(app));
            App.Startup += App_Startup;
        }
    }
}
