using System.Windows;
using System.Windows.Controls;

namespace Brows {
    using Logger;

    partial class GraphicalLogServiceWindow {
        private static readonly GraphicalLogService LogService = new GraphicalLogService {
            DesiredSeverity = LogSeverity.Info
        };

        static GraphicalLogServiceWindow() {
            Logging.Add(LogService);
        }

        private async void This_Loaded(object sender, RoutedEventArgs e) {
            DataContext = LogService;
            for (; ; ) {
                var w = await LogService.Reader.WaitToReadAsync();
                if (w == false) {
                    break;
                }
                var t = LogService.Reader.TryRead(out var s);
                if (t == false) {
                    break;
                }
                Dispatcher.Invoke(delegate {
                    var children = StackPanel.Children;
                    if (children.Count > 1000) {
                        children.RemoveRange(0, children.Count - 1000);
                    }
                    children.Add(new TextBlock { Text = s });
                    ScrollViewer.ScrollToBottom();
                });
            }
        }

        public GraphicalLogServiceWindow() {
            Loaded += This_Loaded;
            InitializeComponent();
        }
    }
}
