using System.Windows;

namespace Brows.Windows {
    internal class AppConfig {
        public AppConfig(Application app) {
            Request.Factory = new CommandFactory();
            CommanderService.Import.Clipboard = new WindowsClipboard();
            CommanderService.Import.DialogFactory = new DialogManagerFactory(app);
        }
    }
}
