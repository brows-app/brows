using System.Windows;

namespace Brows {
    public class DialogManagerFactory : IDialogManagerFactory {
        public Application Application { get; }

        public DialogManagerFactory(Application application) {
            Application = application;
        }

        public IDialogManager Create() {
            return new DialogManager(Application);
        }
    }
}
