using System.Windows.Threading;

namespace Brows {
    partial class Dialog : IDialog {
        public string Key { get; set; }

        public object Data {
            get => Dispatcher.Invoke(() => DataContext);
            set => Dispatcher.Invoke(() => DataContext = value);
        }

        public Dialog() {
            InitializeComponent();
        }
    }
}
