using Domore.Windows.Controls;
using System;

namespace Brows.Gui {
    internal class PreviewController : Controller<IPreviewController>, IPreviewController {
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
            UserControl.PreviewHandlerControl.PreviewHandlerCLSIDChanged += PreviewHandlerControl_PreviewHandlerCLSIDChanged;
        }

        public void Refresh() {
            UserControl.PreviewHandlerControl.PreviewHandlerRefresh = true;
        }
    }
}
