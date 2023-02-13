using Domore.Windows.Controls;
using System;
using System.Windows;

namespace Brows.Gui {
    internal class PreviewController : Controller<IPreviewController>, IPreviewController {
        private void Configure() {
            var config = Config?.Invoke();
            if (config != null) {
                UserControl.PreviewTextControl.PreviewTextOptions = config.TextOptions;
                UserControl.PreviewTextControl.PreviewTextFileLengthMax = config.TextFileLengthMax;
            }

        }

        private void PreviewTextControl_PreviewTextSourceChanged(object sender, RoutedEventArgs e) {
            Configure();
        }

        private void PreviewHandlerControl_PreviewHandlerCLSIDChanged(object sender, PreviewHandlerCLSIDChangedEventArgs e) {
            if (e != null) {
                var config = Config?.Invoke();
                if (config != null) {
                    e.Override =
                        config.CLSID(e.Extension) ??
                        config.CLSID(e.CLSID);
                }
            }
        }

        public Func<IPreviewConfig> Config { get; set; }

        public new PreviewControl UserControl { get; }

        public PreviewController(PreviewControl userControl) : base(userControl) {
            UserControl = userControl ?? throw new ArgumentNullException(nameof(userControl));
            UserControl.PreviewTextControl.PreviewTextSourceChanged += PreviewTextControl_PreviewTextSourceChanged;
            UserControl.PreviewHandlerControl.PreviewHandlerCLSIDChanged += PreviewHandlerControl_PreviewHandlerCLSIDChanged;
        }

        public void Refresh() {
            Configure();
            UserControl.PreviewTextControl.PreviewTextRefresh = true;
            UserControl.PreviewHandlerControl.PreviewHandlerRefresh = true;
        }
    }
}
