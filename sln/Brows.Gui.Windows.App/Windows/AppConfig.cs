using Domore.IO;
using System.Threading;

namespace Brows.Windows {
    internal class AppConfig {
        public AppConfig(WindowsApplication app) {
            Request.Factory = new CommandFactory();
            FileSystemEvent.SynchronizationContext = SynchronizationContext.Current;
            CommanderService.Import.Clipboard = new WindowsClipboard();
            CommanderService.Import.DialogFactory = new DialogManagerFactory(app);
        }
    }
}
