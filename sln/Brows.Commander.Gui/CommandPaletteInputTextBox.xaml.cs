using System;
using System.Windows;
using System.Windows.Threading;

namespace Brows {
    partial class CommandPaletteInputTextBox {
        private void This_Loaded(object sender, RoutedEventArgs e) {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate {
                if (SelectionLength == 0) {
                    CaretIndex = Text.Length;
                }
                Focus();
            });
        }

        public CommandPaletteInputTextBox() {
            Loaded += This_Loaded;
            InitializeComponent();
        }
    }
}
