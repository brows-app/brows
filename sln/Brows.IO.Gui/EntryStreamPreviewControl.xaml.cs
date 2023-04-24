using Domore.Windows.Controls;
using System.Windows;

namespace Brows {
    partial class EntryStreamPreviewControl {
        private void PreviewHandlerControl_PreviewHandlerLoadingChanged(object sender, RoutedEventArgs e) {
            var control = sender as PreviewHandlerControl;
            if (control != null) {
                EntryStreamGui?.State?.Preview?.Change(loading: control.PreviewHandlerLoading);
            }
        }

        private void PreviewHandlerControl_PreviewHandlerSuccessChanged(object sender, RoutedEventArgs e) {
            var control = sender as PreviewHandlerControl;
            if (control != null) {
                EntryStreamGui?.State?.Preview?.Change(success: control.PreviewHandlerSuccess);
            }
        }

        private void PreviewHandlerControl_PreviewHandlerCLSIDChanged(object sender, PreviewHandlerCLSIDChangedEventArgs e) {
            var clsid = EntryStreamGui?.Options?.PreviewCLSID;
            if (clsid != null) {
                if (clsid.TryGetValue(e.Extension, out var extensionOverride)) {
                    e.Override = extensionOverride;
                }
                else {
                    if (clsid.TryGetValue($"{e.CLSID}", out var clsidOverride)) {
                        e.Override = clsidOverride;
                    }
                }
            }
        }

        protected override void OnEntryStreamGuiChanged(DependencyPropertyChangedEventArgs e) {
            PreviewHandlerControl.PreviewHandlerSource = EntryStreamGui?.Source?.SourceFile;
            base.OnEntryStreamGuiChanged(e);
        }

        public EntryStreamPreviewControl() {
            InitializeComponent();
        }
    }
}
