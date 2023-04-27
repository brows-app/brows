using Domore.Windows.Controls;
using System.Windows;

namespace Brows {
    partial class EntryStreamPreviewControl {
        private void PreviewHandlerControl_Loaded(object sender, RoutedEventArgs e) {
            ChangeEntryStreamGui();
        }

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

        private void ChangeEntryStreamGui() {
            PreviewHandlerControl.PreviewHandlerSource = EntryStreamGui?.Source?.SourceFile;
        }

        protected sealed override void OnEntryStreamGuiChanged(DependencyPropertyChangedEventArgs e) {
            ChangeEntryStreamGui();
            base.OnEntryStreamGuiChanged(e);
        }

        public EntryStreamPreviewControl() {
            InitializeComponent();
        }
    }
}
